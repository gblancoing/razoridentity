# Sprint 1 Smoke Tests

## Objetivo

Validar los flujos base de autenticacion, onboarding partner y geografia publica
despues de los ajustes de estabilizacion.

## Precondiciones

- `ComunaClick.Acl` corriendo en `http://localhost:5135`
- `ComunaClick.Api` corriendo en `http://localhost:5277`
- `Payments.Gateway.Api` corriendo en `http://localhost:5207`
- Base de datos accesible
- Seed de demo/QA cargado si corresponde

## 1. Geo publico

1. `GET /v1/public/geo/countries`
   Resultado esperado: lista no vacia de paises activos.
2. `GET /v1/public/geo/regions?countryId=<id>`
   Resultado esperado: lista de regiones activas del pais.
3. `GET /v1/public/geo/comunas?regionId=<id>`
   Resultado esperado: lista de comunas activas de la region.
4. `GET /v1/public/geo/tenant-by-comuna/<comunaId>`
   Resultado esperado: retorna `TenantId`, `ComunaId`, nombres geo y no responde 404.
5. `POST /v1/public/geo/tenant-by-comuna/<comunaId>`
   Resultado esperado: mismo resultado que GET.

## 2. Login y selector de negocios

1. Iniciar sesion desde `/login` con usuario valido.
   Resultado esperado: token emitido con `tenant_id` y sin `partner_id` fijo incorrecto.
2. Navegar a `/my-businesses`.
   Resultado esperado: lista de partners asociados al tenant o estado vacio correcto.
3. Entrar a un negocio desde `/my-businesses`.
   Resultado esperado: refresh conserva `tenantId` y asigna `partnerId` del negocio elegido.

## 3. Onboarding de partner

1. Crear partner tipo A desde `/register/business`.
   Resultado esperado: partner creado, `IsVisible = false`, activacion incompleta con falta de producto.
2. Crear partner tipo B.
   Resultado esperado: activacion incompleta con falta de servicio.
3. Crear partner tipo C.
   Resultado esperado: activacion incompleta con falta de profesional.
4. Revisar `GET /v1/partners/mine`.
   Resultado esperado: devuelve partners del tenant y, si hay partner en sesion, respeta el scope.

## 4. Quickstart operativo

1. Partner A: crear primer producto.
   Resultado esperado: checklist mejora y catalogo muestra item.
2. Partner B: crear primer servicio.
   Resultado esperado: checklist mejora y agenda ya tiene oferta base.
3. Partner C: crear primer profesional.
   Resultado esperado: checklist mejora y quickstart redirige correctamente.

## 5. Pago interno base

1. Crear payment en Core API.
   Resultado esperado: payment `pending`.
2. Simular notify desde gateway con `externalReference`.
   Resultado esperado: payment cambia de estado y se registra `PaymentEvent`.
3. Repetir notify con mismo `providerEventId`.
   Resultado esperado: respuesta `duplicate`.

## Criterio de cierre Sprint 1

- No hay errores de `TenantId is required` en onboarding valido.
- `public/geo` responde igual en ambiente local y publicado.
- Login no fija un `partnerId` demo por defecto.
- Refresh conserva scope actual.
- Webhook interno de pagos resuelve correctamente el tenant desde el payment real.
