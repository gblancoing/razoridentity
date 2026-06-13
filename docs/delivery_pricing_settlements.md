# Tarifa dinámica de envío + liquidación al transportista

## Cálculo de la tarifa (server-side, parametrizado)

- Servicio: `IDeliveryFeeCalculator` / `DeliveryFeeCalculator` (`Modules/Delivery`).
- Distancia: `GeoDistance.CalculateDistanceKm` (Haversine) entre el local del
  Partner y el pin del comprador (coords ya persistidas en la orden).
- Perfiles horarios en `DeliveryPricing` (appsettings): se elige el primero
  cuya ventana contiene la **hora local de Chile** (`America/Santiago`, con
  fallback `Pacific SA Standard Time`; nunca UTC). Por defecto:
  - `nocturno` (00:00–07:00): base $3.500 hasta 2 km, $1.600 por km excedente.
  - `diurno` (07:00–24:00): base $2.800 hasta 2 km, $1.250 por km excedente.
- **Km excedente = `ceil(distancia − radioBase)`**: toda fracción de km
  iniciado se cobra como km entero (decisión de negocio).
- Tope `MaxFee` (default $15.000) contra coordenadas erróneas; radio máximo de
  cobertura `MaxDistanceKm` (default 15 km): si se excede, el checkout rechaza
  la compra con mensaje claro (`DeliveryOutOfRangeException`).
- Coordenadas faltantes/ inválidas (`GeoDistance.IsValidCoordinate`): **fallback**
  a la tarifa plana por zona (`Delivery:FlatFee` → `DeliveryProvider.BaseFee`),
  no se rechaza la compra.
- Integración: `DynamicDeliveryPricingService : IDeliveryPricingService`,
  inyectado en `OrderCheckoutService`. El cliente nunca define el `DeliveryFee`
  (H7 cerrado). Redondeo CLP con `FeeCalculator.RoundClp`.

## Comisiones y cálculo del neto al transportista

### Tasas vigentes (configuradas en `DeliveryPricing:Commission` en appsettings)

| Concepto | Tasa | Sobre |
|---|---|---|
| Comisión ComunaClic (`PercentageFee`) | **2,61%** | Bruto del envío |
| Tasa MercadoPago (`MercadoPagoFeeRate`) | **3,49%** | Bruto del envío |
| **Total descuentos** | **6,10%** | |

### Fórmula

```
netoTransportista = brutoEnvío − (brutoEnvío × 2,61%) − (brutoEnvío × 3,49%)
```

**Ejemplo real para envío de $2.800:**

| Concepto | Monto |
|---|---|
| Bruto cobrado al cliente | $2.800 |
| − Comisión ComunaClic (2,61%) | −$73 |
| − Comisión MercadoPago (3,49%) | −$98 |
| **Neto al transportista** | **$2.629** |

### Notas sobre el fee MercadoPago

- **Pagos vía MP (Checkout Pro):** el fee real llega por webhook (`fee_details`)
  y se concilia en `ReconcileMercadoPagoFeeAsync`, prorrateado por el peso del
  envío dentro del bruto total del pago (`feeMP × envío / brutoPago`). El valor
  final reemplaza la estimación inicial.
- **Pagos manuales (efectivo/transferencia):** MP ya cobró su tasa cuando el
  comprador pagó. Se aplica la `MercadoPagoFeeRate` configurada (3,49%) como
  estimación definitiva desde la creación del settlement.
- El **residuo de redondeo CLP se asigna siempre al neto del transportista**:
  `bruto = feeMP + feeCC + neto` cuadra exacto.

### Dónde está configurado

Archivo: `src/ComunaClick.Api/appsettings.json`

```json
"DeliveryPricing": {
  "Commission": {
    "FixedFeeAmount": 0,
    "PercentageFee": 2.61,
    "MercadoPagoFeeRate": 3.49
  }
}
```

> **Importante:** el deploy script restaura `appsettings.json` en el servidor
> desde el backup. Si se actualizan las tasas, editar también el archivo en el
> servidor vía SSH para que el cambio sea inmediato, y actualizar el backup:
> ```bash
> sudo python3 -c "import json; ..."   # ver deploy_comunaclic.sh
> sudo systemctl restart comunaclic-api.service
> ```

## El transportista como prestador con cuenta MercadoPago

Decisión de modelado: el courier obtiene su **propia fila `Seller` con
`Id = Courier.Id`** (mismo patrón Id-compartido que Partner→Seller). Eso
reutiliza COMPLETO el flujo OAuth de MercadoPago, el cifrado de tokens
(`ISecretProtector`) y el estado *checkout ready* existentes, sin duplicar
lógica ni tablas. `couriers.kind` (`courier` | `taxi` | …) deja el tipo
extensible.

- `ICourierPayeeService.EnsurePayeeSellerAsync` crea la fila Seller.
- `POST /v1/partners/{pid}/couriers/{cid}/mercadopago/start` devuelve la URL de
  autorización; el negocio se la comparte al repartidor, que la abre con SU
  cuenta MP. `GET …/mercadopago/status` muestra el estado.

## Liquidación del envío (slip) — pago separado, no doble checkout

Decisión documentada: **MercadoPago marketplace admite UN solo collector por
pago**. Cobrar el envío en un checkout aparte obligaría al comprador a pagar
dos veces. Por eso el comprador paga UNA vez (collector = comercio) y la
liquidación del transportista queda como **registro separado y auditable**
(`core.delivery_settlements`, 1 por orden).

### Estados del slip

```
pending ──► processing ──► settled          (flujo vía MercadoPago)
   │
   └──► manual ──► manual_confirming ──► settled   (flujo pago directo)
```

| Estado | Descripción |
|---|---|
| `pending` | Courier con cuenta MP *checkout ready*; esperando que el comercio pague. |
| `processing` | Link de Checkout Pro creado; esperando aprobación de MP. |
| `manual` | Courier sin cuenta MP; el comercio debe pagar directamente (efectivo/transferencia). |
| `manual_confirming` | Comercio declaró que ya pagó; esperando confirmación del transportista. |
| `settled` | Pago confirmado (por MP webhook o por el transportista manualmente). |

### Flujo pago vía MercadoPago (courier con cuenta MP)

1. Comercio ve tarjeta "Liquidación del envío" en el pedido entregado.
2. Clic en **"Pagar envío al repartidor"** → se genera link de Checkout Pro.
3. Comprador paga en pestaña nueva; el webhook MP liquida el slip automáticamente.
4. El fee MP real del webhook reemplaza la estimación; neto recalculado → `settled`.

### Flujo pago manual (courier sin cuenta MP o preferencia del comercio)

1. Comercio hace clic en **"Declarar pago (efectivo / transferencia)"**.
2. Selecciona método de pago y agrega nota opcional (ej. número de transferencia).
3. El slip pasa a `manual_confirming`; el transportista recibe aviso en su panel.
4. En "Mis ganancias" el transportista ve la fila en amarillo con el monto y la nota.
5. Clic en **"Confirmar recibo"** → el slip pasa a `settled` (verde).

### Idempotencia y recálculo

`CreateForPaidOrderAsync` es idempotente: si ya existe un slip para la orden,
lo retorna. **Excepción:** si el slip existente está en estado `pending` o
`manual` (no confirmado), recalcula las comisiones con las tasas actuales de
`appsettings` antes de retornarlo. Esto corrige automáticamente slips creados
con tasas de configuración incorrectas.

### Consultas por rol

- **Comercio:** `GET /v1/partners/{partnerId}/delivery-settlements` (`partner.staff`)
- **Admin:** `GET /v1/admin/delivery-settlements?status=` (`platform.admin`)
- **Transportista (portal):** `GET /v1/courier/me/earnings` y `GET /v1/courier/me/trips`
- **Transportista (sin cuenta):** `GET /api/courier/settlement?orderId&token=` con token seguro del reparto

## Pago del envío al repartidor (comercio → courier vía MercadoPago)

El cliente paga UNA sola vez (al comercio, productos + envío). Tras la entrega,
el comercio paga el envío al transportista con un **link de Checkout Pro donde
el collector es el repartidor** (su cuenta MP vinculada):

- Monto que paga el comercio = **bruto del envío** (lo que ya cobró al cliente).
- Del pago salen `application_fee` = comisión CC del transporte y el fee MP de
  ESA transacción (lo absorbe el collector, como en cualquier venta MP).
- El neto cae **directo en la cuenta MP del repartidor**, sin pasos manuales.
- El webhook (`external_reference` con prefijo `cc-delivery-{settlementId}`)
  liquida el slip automáticamente: el **fee MP real reemplaza el prorrateo
  estimado**, recalcula el neto (residuo de redondeo al neto) y marca `settled`.
  Un pago rechazado/cancelado devuelve el slip a `pending` (se puede reintentar).

### Endpoint

`POST /v1/partners/{partnerId}/delivery-settlements/{id}/pay` (`partner.staff`)
→ `{ initPoint }`. Idempotente: con un pago pendiente devuelve el mismo link;
tras un rechazo reutiliza la misma fila Payment con una preference nueva.

### Nota contable

El `application_fee` del comercio se calcula sobre el bruto TOTAL del pago
(productos + envío). Si la comisión del transporte (`DeliveryPricing:Commission`)
es igual al porcentaje del comercio, el traspaso del neto al courier deja al
comercio exactamente con el neto de sus productos (los descuentos se cancelan).
Si difieren, el delta queda en el comercio — calibrar ambas comisiones juntas.

## Clases clave

| Clase / Interfaz | Ubicación | Rol |
|---|---|---|
| `DeliveryPricingOptions` | `Api/Configuration/` | Opciones de tarificación dinámica + comisiones |
| `DeliveryCommissionOptions` | `Api/Configuration/` | `PercentageFee` (CC) + `MercadoPagoFeeRate` |
| `FeeCalculator` | `Api/Modules/Marketplace/` | `Calculate(gross, fixed, pct)` + `RoundClp` |
| `DeliverySettlementService` | `Api/Modules/Delivery/` | CRUD de slips, recálculo, flujo manual |
| `DeliverySettlementsController` | `Api/Modules/Delivery/` | Endpoints partner + admin |
| `CourierPortalController` | `Api/Modules/Delivery/` | Ganancias, viajes, confirmación recibo |
| `PartnerOrderDeliverySection` | `SharedUI/Components/Partner/` | UI comercio: modal pago, desglose |
| `CourierPanel` | `SharedUI/Pages/Account/` | UI transportista: "Mis ganancias" |
