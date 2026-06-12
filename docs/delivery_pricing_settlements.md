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
(`core.delivery_settlements`, 1 por orden):

```
netoTransportista = brutoEnvío − feeMP(prorrateado) − comisiónComunaClic
```

- Comisión CC del transporte: `FeeCalculator.Calculate` con
  `DeliveryPricing:Commission` (fijo + %, default 10%).
- Fee MP: NO se inventa; se concilia con el `fee_details` real del webhook
  (excluyendo `application_fee`) **prorrateado** por el peso del envío dentro
  del bruto del pago (`feeMP × envío / brutoPago`).
- El **residuo de redondeo CLP se asigna siempre al neto del transportista**:
  `bruto = feeMP + feeCC + neto` cuadra exacto (verificado por tests).
- Pago manual (transferencia/efectivo, sin MP): slip con `feeMP = 0`.
- Estados: `pending` (liquidable vía MP), `manual` (courier sin cuenta MP
  *checkout ready* → pago directo marcado como tal), `settled` (transferido;
  `POST /v1/admin/delivery-settlements/{id}/settle`).
- El slip se crea al confirmarse el pago (webhook MP o mark-paid manual) y se
  vincula al courier al asignarlo (la asignación puede ser posterior al pago).

### Consulta (autorización)

- Negocio: `GET /v1/partners/{partnerId}/delivery-settlements` (partner.staff).
- Admin: `GET /v1/admin/delivery-settlements?status=` (platform.admin).
- Transportista: `GET /api/courier/settlement?orderId&token=` con el mismo
  token seguro del reparto (sin cuenta de usuario).

### Nota contable (importante para configurar la comisión)

El `application_fee` del comercio hoy se calcula sobre el bruto TOTAL del pago
(productos + envío). Si la comisión del transporte (`DeliveryPricing:Commission`)
es igual al porcentaje del comercio, el traspaso del neto al courier deja al
comercio exactamente con el neto de sus productos (los descuentos se cancelan).
Si difieren, el delta queda en el comercio — calibrar ambas comisiones juntas.

### Futuro

El traspaso efectivo del dinero al courier (hoy `settled` manual desde admin)
puede automatizarse con la API de money-transfer de MP cuando se habilite;
el destino ya queda verificado por el OAuth (cuenta conectada del courier).
