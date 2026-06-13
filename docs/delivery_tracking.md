# Delivery con tracking GPS en vivo

## Resumen del módulo

- El comprador elige envío con dirección en el checkout y fija un **pin de destino** (Leaflet + Nominatim, sin API key).
- El vendedor registra **repartidores propios** (Pedidos → "Mis repartidores") y al asignar uno a un pedido se genera un **link seguro** (token HMAC con TTL 24-48h) que comparte por WhatsApp.
- El repartidor abre el link (`/courier/delivery/{orderId}?token=…`), aprieta "Recogí el pedido" y su GPS se transmite cada ~5s (`watchPosition` + Wake Lock).
- El comprador sigue el envío en `/track/{orderId}?token=…`: mapa con origen/destino/repartidor en vivo vía **SignalR** (`/deliveryHub` en el API) con fallback a polling.

## Seguridad

- Token del repartidor: HMAC con propósito `courier` y la clave `orders.courier_token_key` embebida. **Reasignar, entregar o cancelar revoca** los tokens anteriores (solo 1 activo por orden).
- Unirse al grupo SignalR (`order-{id}`) exige token válido (de comprador o repartidor).
- `POST /api/courier/location`: valida rangos lat/lng, descarta timestamps regresivos y omite persistir puntos a <15 m (igual difunde). Rate limit `courier-gps` particionado **por token**.
- Retención: `DeliveryTrackingCleanupJob` (tick de `JobsHostedService`) borra el historial de envíos terminados hace >7 días conservando el último punto.

## Configuración (`Delivery` en appsettings)

| Clave | Default | Uso |
|---|---|---|
| `FlatFee` | null | Tarifa plana provisional; null ⇒ `BaseFee` del proveedor (la fórmula por km llegará detrás de `IDeliveryPricingService`) |
| `CourierTokenTtlHours` | 48 | Vigencia del link del repartidor |
| `AppBaseUrl` | https://app.comunaclic.cl | Base del link del repartidor |
| `MinPersistDistanceMeters` | 15 | Umbral de persistencia del historial GPS |
| `TrackingRetentionDays` | 7 | Retención del historial tras entregar/cancelar |

El token del repartidor reutiliza el secreto `OrderTracking:TrackingTokenSecret` (con propósito distinto), así que **no requiere secretos nuevos** en producción.

## Deploy

1. La migración `infra/migrations/20260612_delivery_tracking.sql` se aplica sola al arrancar la API (`DatabaseSchemaBootstrap` detecta que falta `core.couriers`).
2. **nginx (api.comunaclic.cl)**: el hub SignalR necesita upgrade de websocket. En el `location /` del vhost del API agregar:

```nginx
proxy_http_version 1.1;
proxy_set_header Upgrade $http_upgrade;
proxy_set_header Connection "upgrade";
```

(Si falta, SignalR cae automáticamente a long-polling — funciona, pero websocket es más eficiente. El fallback final de la página es polling del snapshot cada ~12s.)
3. CSP de producción ya permite `wss:` y `https://api.comunaclic.cl` en `connect-src`; CORS del API ya incluye app.comunaclic.cl. Sin claves nuevas (Leaflet/OSM/Nominatim no requieren API key; cliente SignalR vendoreado en `lib/signalr/`).

## Escalamiento (futuro, NO implementado)

Hoy el API corre en **1 instancia** y los grupos SignalR viven en memoria. Si algún día hay más de una instancia detrás de nginx:

- Backplane Redis: `builder.Services.AddSignalR().AddStackExchangeRedis("<conn>")`.
- Sticky sessions en nginx (`ip_hash` o cookie) para las conexiones del hub.

## Próxima tarea anotada

- Tarifa por distancia/kilómetro: implementar nueva clase `IDeliveryPricingService` usando `DeliveryPricingContext` (ya trae origen y destino de la orden) y registrar en `Program.cs` en lugar de `FlatRateDeliveryPricingService`.
