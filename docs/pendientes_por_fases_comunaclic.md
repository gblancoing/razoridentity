# Pendientes Por Fases - ComunaClic

## Objetivo

Ordenar el trabajo pendiente en etapas cortas y publicables, priorizando primero el cierre end-to-end funcional y después seguridad, calidad y mejoras de experiencia.

## Fase 1 - Cierre End-To-End Buyer y Partner

### Pendientes

- [ ] Validar en producción el flujo partner completo con usuario nuevo: registro, creación de negocio, carga de oferta, publicación y visibilidad pública.
- [ ] Validar en producción el flujo buyer completo con cuenta personal: registro, login, navegación por categorías, detalle, contacto, reserva y compra.
- [ ] Completar el flujo real de compra y reserva si todavía hay pasos simulados o no persistidos.
- [x] Completar estados de confirmación y error post-compra/post-reserva/contacto para que el usuario entienda el resultado de la operación y tenga navegación directa a seguimiento o retorno.
- [x] Mejorar la ficha pública de negocio, producto, servicio y profesional con datos clave por tipo, hero visual con fallback por categoría y CTA más claros.

### Criterio De Cierre

Un usuario partner puede publicar una oferta y un usuario buyer puede encontrarla, contactarla, reservarla o comprarla sin intervención manual.

### Publicación

- Servicios esperados: `api`, `app`
- Publicar al terminar cada bloque funcional validado.

## Fase 2 - Geolocalización y Descubrimiento Cercano

### Pendientes

- [ ] Permitir que el partner edite su ubicación exacta después del onboarding.
- [ ] Mostrar en panel partner si la ubicación usada es exacta o referencia por comuna.
- [ ] Mejorar la UX móvil del mapa por categoría.
- [ ] Agregar fallback visual claro si el usuario niega geolocalización o si el navegador no entrega coordenadas.
- [ ] Evaluar clustering de marcadores si una categoría tiene muchos negocios cerca.

### Criterio De Cierre

El buyer puede explorar una categoría en mapa con ubicación cercana confiable y el partner puede mantener su ubicación de negocio actualizada.

### Publicación

- Servicios esperados: `api`, `app`
- Publicar primero backend de ubicación y luego frontend de mapa si los cambios no son simultáneos.

## Fase 3 - Seguridad y Control de Acceso

### Pendientes

- [ ] Ejecutar rotación de secretos productivos y mover credenciales sensibles fuera de `appsettings.json`.
- [ ] Revisar todas las políticas de autorización y ownership en endpoints partner/admin para evitar acceso cruzado.
- [ ] Endurecer endpoints públicos de tracking para que no expongan información sensible solo por conocer un GUID.
- [ ] Revisar si `PasswordHasher` puede dejar de aceptar hash legacy o texto plano y definir plan de migración.
- [ ] Confirmar comportamiento real de rate limiting y reCAPTCHA en producción con pruebas de abuso controladas.

### Criterio De Cierre

Credenciales fuera del repo, permisos consistentes, endpoints públicos con exposición mínima y protección antiabuso verificada.

### Publicación

- Servicios esperados: `acl`, `api`, eventualmente `app` si cambia UX de login/registro o manejo de errores.

## Fase 4 - Operación Partner: Agenda, Leads, Payouts y Notificaciones

### Pendientes

- [ ] Profundizar `Agenda` con datos operativos reales, estados accionables y filtros.
- [ ] Mejorar `Leads` con seguimiento, estado, asignación y acciones rápidas.
- [ ] Mejorar `Payouts` con trazabilidad, filtros, totales y estados claros.
- [ ] Mejorar `Notifications` con lectura, priorización, filtros y UX consistente.
- [ ] Agregar estados vacíos y mensajes de error por módulo para no dejar pantallas ambiguas.

### Criterio De Cierre

El panel partner deja de ser solo visualización segura y pasa a ser una herramienta operativa diaria.

### Publicación

- Servicios esperados: `api`, `app`

## Fase 5 - Calidad Técnica y Automatización

### Pendientes

- [ ] Agregar pruebas automáticas mínimas para login, registro, publicación, categorías, tracking y ownership.
- [ ] Crear smoke test post-deploy reproducible para `app`, `api` y `acl`.
- [ ] Investigar y corregir la lentitud o cuelgues intermitentes de `dotnet build` / `dotnet publish` en entorno local.
- [ ] Revisar warnings restantes y deuda de nullability.
- [ ] Documentar checklist de release y rollback por servicio.

### Criterio De Cierre

Cada publicación tiene validación repetible, menor riesgo regresivo y menor fricción operativa.

### Publicación

- Servicios esperados: según el módulo afectado. No desplegar servicios sin cambios.

## Fase 6 - Orden de Repo y Documentación

### Pendientes

- [ ] Limpiar archivos locales sueltos y temporales sin afectar trabajo útil.
- [ ] Normalizar documentación técnica y funcional en `docs/` y `README.md`.
- [ ] Documentar matriz de ambientes, endpoints, credenciales por variable de entorno y flujo de deploy.
- [ ] Mantener bitácora diaria en `memory/YYYY-MM-DD.md` y actualizar documentación cuando una fase se cierre.

### Criterio De Cierre

El repo queda entendible, reproducible y con trazabilidad clara de lo implementado y lo pendiente.

### Publicación

- Servicios esperados: normalmente ninguno si solo cambia documentación. Publicar a GitHub con commit/push.

## Orden De Ejecución Recomendado

1. Fase 1
2. Fase 2
3. Fase 3
4. Fase 4
5. Fase 5
6. Fase 6

## Nota Operativa

Para respetar la regla de despliegue actual, cada publicación debe incluir solo los servicios que realmente cambiaron: `api`, `acl`, `app` o `site`.
