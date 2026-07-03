Necesito que hagas un hardening de autenticación en este repo .NET/Blazor, reduciendo la exposición de tokens en navegador.

Contexto:
- Hoy la app web guarda `AccessToken` y `RefreshToken` en `localStorage`.
- Queremos mejorar seguridad sin rehacer toda la arquitectura a un BFF completo.
- El objetivo es implementar una solución incremental y de bajo riesgo operativo.

Objetivo principal:
- Eliminar la persistencia del `RefreshToken` en `localStorage`.
- Mantener el `AccessToken` solo en memoria o, si hace falta por UX, en `sessionStorage` en vez de `localStorage`.
- Diseñar el refresh de sesión usando una cookie `HttpOnly`, `Secure`, `SameSite` adecuada, emitida por backend.
- Mantener compatibilidad razonable con el flujo actual de login, refresh, logout y llamadas API.

Qué espero que hagas:
1. Audita el flujo actual de autenticación en estos puntos:
   - emisión de tokens en ACL/login
   - refresh token
   - logout
   - almacenamiento frontend (`WebTokenStore`, `app.js`, servicios de auth)
   - clientes HTTP que adjuntan bearer
2. Propón e implementa la opción incremental más segura posible, evitando un rediseño total.
3. Cambia el backend para que el refresh token no viaje ni quede accesible desde JS.
4. Ajusta frontend para:
   - no guardar `RefreshToken` en `localStorage`
   - usar memoria o `sessionStorage` para `AccessToken`
   - seguir refrescando sesión sin romper UX
5. Revisa impactos en:
   - cookies
   - CSRF/antiforgery
   - CORS
   - expiración de sesión
   - logout
6. Si hay decisiones no obvias, elige la alternativa menos disruptiva y explícala.
7. Agrega comentarios breves solo donde realmente aclaren algo no evidente.
8. Verifica con pruebas o checks razonables y reporta qué pudiste validar y qué no.

Restricciones:
- No hagas un BFF completo salvo que sea estrictamente necesario.
- No dejes tokens sensibles accesibles por `localStorage`.
- No uses soluciones parciales que solo cambien `localStorage` por otra cosa insegura sin justificarlo.
- Preserva el comportamiento actual lo más posible.

Entregables:
- Cambios de código listos en el repo
- Resumen corto de la estrategia elegida
- Riesgos residuales
- Lista de archivos tocados
- Qué faltaría para una fase 2 más robusta
