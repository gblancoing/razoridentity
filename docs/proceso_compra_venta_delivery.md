# Proceso de compra y venta con servicio de delivery — ComunaClic

Documento de referencia del flujo end-to-end de una venta con envío a domicilio:
desde que el comprador paga hasta que el comercio le paga el envío al repartidor.
Incluye los cuatro actores (comprador, comercio, repartidor, administrador),
los montos/comisiones y los estados de cada etapa.

---

## 1. Actores y dónde opera cada uno

| Actor | Dónde entra | Qué hace |
|-------|-------------|----------|
| **Comprador** | app.comunaclic.cl | Compra productos (carrito multi-negocio), elige envío a domicilio, paga, sigue su pedido en el mapa. |
| **Comercio** | app.comunaclic.cl → panel `/partner` | Vende, asigna repartidor, sigue el envío, agradece la entrega, paga el envío al repartidor (Liquidaciones). |
| **Repartidor** | app.comunaclic.cl → `/account/courier` | Crea su cuenta, ve sus viajes y ganancias, vincula su Mercado Pago para cobrar los envíos. |
| **Administrador** | admin.comunaclic.cl | Crea los transportistas por zona (comuna/región/global) y marca liquidaciones manuales como pagadas. |

---

## 2. Requisitos previos (configuración)

1. **Transportistas por zona** (admin → `/delivery-providers`): definen la *tarifa de envío*.
   Se asignan por comuna > región > global. Sin un transportista que cubra la zona
   del comercio, el envío cotiza **$0** (no se cobra).
2. **Coordenadas del comercio** (lat/lng): con ellas aplica la **tarifa dinámica por
   distancia y horario**; sin ellas, la **tarifa plana** (BaseFee del transportista).
3. **Transportista preferido del comercio** (opcional, panel del comercio →
   Configuración de empresa → Despacho): si no elige, se asigna automático por zona.

### Tarifa dinámica (config `DeliveryPricing`)
- Diurno (07:00–24:00): base $2.800 + $1.250/km excedente del radio base.
- Nocturno (00:00–07:00): base $3.500 + $1.600/km.
- Radio base: 2 km. Radio máx. de cobertura: 15 km. Tope: $15.000.
- Hora local de Chile (no UTC).

---

## 3. Flujo del comprador (compra)

1. Agrega productos al **carrito** (puede ser de varios negocios → un pedido por negocio).
2. En el checkout elige **"Envío a domicilio"** y **fija el pin** de su ubicación.
3. El sistema **cotiza el envío en vivo** y muestra: Subtotal + Costo de envío + Total,
   con el nombre del transportista. Si la dirección está fuera de cobertura, lo bloquea.
4. Paga con **Mercado Pago**. La pasarela cobra el **total con envío incluido**
   (producto + recargo de transporte).
5. Recibe enlace de **seguimiento en vivo** (mapa con local 🏪, destino 🏠 y repartidor 🛵).

> El comprador paga **una sola vez** al comercio: productos + envío juntos.

### Estados del pedido
`payment_pending` → `paid` (al confirmar Mercado Pago) → puede terminar en `cancelled`.
Una orden `payment_pending` reserva stock y **expira a los 5 minutos** si no se paga
(libera el stock); si el pago llega después, el pago confirmado gana.

---

## 4. Flujo del comercio (venta + envío)

1. En **Pedidos** ve la venta pagada y la **línea de tiempo del envío**:
   Pendiente → Repartidor asignado → En camino → Entregado.
2. Registra a sus **repartidores** en "Mis repartidores" (nombre, teléfono, y **correo**
   para que el repartidor pueda crear su cuenta).
3. **Asigna un repartidor** al pedido → se genera un **link seguro** para compartirle por
   WhatsApp. El repartidor abre ese link en su celular, activa GPS y actualiza el estado.
4. El comercio puede **"Ver recorrido en vivo"** (mismo mapa que el comprador).
5. Al entregar, aparece **"Agradecer entrega por WhatsApp"** con un mensaje listo.

### Indicadores del repartidor (en "Mis repartidores")
- **"Cuenta creada"** = el repartidor reclamó su portal (ve viajes/ganancias).
- **"MP conectado" / "MP sin conectar"** = si ya se le puede pagar el envío por Mercado Pago.

---

## 5. Liquidación del envío (comercio → repartidor)

Cuando el pedido con delivery queda **pagado**, se genera una **liquidación** (slip) con
el desglose del transporte. Aparece en **Pedidos → "Liquidaciones de envío"**.

### Desglose (ejemplo real: envío cobrado $2.800)

| Concepto | Monto |
|----------|-------|
| Envío cobrado al cliente (bruto) | $2.800 |
| − Comisión ComunaClic (10%) | −$280 |
| − Comisión Mercado Pago (estimada) | −$86 |
| **Neto al repartidor** | **$2.434** |

> El cliente pagó productos + envío al comercio. La liquidación es el pago **separado**
> del comercio al repartidor por el transporte. El comercio y el repartidor se liquidan
> por vías distintas aunque vengan del mismo pedido.

### El comercio NO recibe un link del repartidor: es el comercio quien paga

El pago al repartidor se **habilita tras la entrega**. Dos caminos según el estado:

- **Estado "pendiente"** (el repartidor tiene Mercado Pago conectado):
  el comercio aprieta **"Pagar por Mercado Pago"** → se genera un checkout donde la
  cuenta MP del repartidor **cobra** y la comisión ComunaClic viaja como `application_fee`
  (se retiene automáticamente). El **neto cae directo en la cuenta del repartidor**.
  El slip pasa a "en proceso" y se marca "pagado" cuando Mercado Pago confirma (webhook).

- **Estado "manual"** (el repartidor NO tenía Mercado Pago conectado al momento del pago):
  el comercio le paga **por transferencia o efectivo** (fuera de la plataforma). Un
  **administrador** marca la liquidación como "liquidada" para dejar registro.
  Si el repartidor conecta su Mercado Pago después, el botón "Pagar por Mercado Pago"
  **igual funciona** (el sistema revalida la cuenta en vivo).

La tabla muestra: Pedido · Fecha · Monto productos · Transporte · Comisión CC ·
Comisión MP · Neto repartidor · Estado · Acción, con totales de pendiente vs pagado.

---

## 6. Flujo del repartidor (portal propio)

1. El comercio lo registra con su **correo** en "Mis repartidores".
2. El repartidor crea su cuenta en **app.comunaclic.cl → Registrarse → "Repartidor"**
   con **ese mismo correo** → su perfil se vincula automáticamente.
3. En su menú aparece **"Panel repartidor"** (`/account/courier`) con:
   - **Mis viajes**: entregas activas y completadas, con estado y neto por viaje.
   - **Mis ganancias**: Pendiente de pago / Pagado / Pago directo + movimientos recientes.
   - **Cuenta Mercado Pago**: **"Vincular mi Mercado Pago"** (y **"Desvincular"** si se
     vinculó la cuenta equivocada — conviene cerrar sesión en mercadopago.com antes de
     re-vincular).

> El mismo repartidor puede trabajar para varios negocios (varios perfiles con el mismo
> correo); el panel agrupa todo y muestra el Mercado Pago por negocio.

---

## 7. Resumen del dinero

```
Comprador ──(productos + envío, 1 pago MP)──► Comercio
                                                  │
                                                  │ (tras la entrega)
                                                  ▼
Comercio ──(envío bruto vía MP; collector = repartidor; ──► Repartidor
            application_fee = comisión ComunaClic)            (neto en su cuenta MP)
                                                  │
                                                  └─ o pago manual (transferencia/efectivo)
                                                     si el repartidor no tiene MP
```

- ComunaClic retiene su comisión del producto (en el pago del comprador) y del transporte
  (en la liquidación del repartidor) como `application_fee` de Mercado Pago.
- El repartidor recibe el **neto** del envío (bruto − comisión ComunaClic − fee real MP).

---

## 8. Referencias de código

| Pieza | Archivo |
|-------|---------|
| Cotización de envío | `src/ComunaClick.Api/Modules/Delivery/DeliveryQuoteService.cs` |
| Cobro al comprador (incluye envío) | `src/ComunaClick.Api/Modules/Marketplace/MarketplacePaymentService.cs` |
| Creación de la liquidación | `src/ComunaClick.Api/Modules/Delivery/DeliverySettlementService.cs` |
| Pago comercio → repartidor | `src/ComunaClick.Api/Modules/Delivery/DeliverySettlementPaymentService.cs` |
| Panel de liquidaciones (comercio) | `src/ComunaClick.SharedUI/Components/Partner/PartnerDeliverySettlementsPanel.razor` |
| Portal del repartidor | `src/ComunaClick.SharedUI/Pages/Account/CourierPanel.razor` + `Modules/Delivery/CourierPortalController.cs` |
| Seguimiento en vivo | `src/ComunaClick.SharedUI/Pages/Buyer/Track.razor` + `wwwroot/delivery-tracking.js` |
