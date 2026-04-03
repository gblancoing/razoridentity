# Secret Rotation Checklist

## Objetivo

Rotar secretos expuestos en configuración sin cortar producción ni invalidar sesiones más allá de lo planificado.

## Secretos a rotar

- `src/ComunaClick.Api/appsettings.json`
  - `ConnectionStrings:CoreDb`
  - `Jwt:SigningKey`
  - `Payments:InternalWebhookKey`
- `src/ComunaClick.Acl/appsettings.json`
  - `ConnectionStrings:AclDb`
  - `Jwt:SigningKey`
  - `Jwt:RefreshTokenPepper`
  - `PasswordReset:TokenPepper`

## Preparación

1. Crear nuevos secretos en el store de producción:
   - AWS SSM Parameter Store o AWS Secrets Manager
   - nombres separados por servicio: `comunaclic/api/*` y `comunaclic/acl/*`
2. Confirmar cómo se inyectan hoy variables de entorno en:
   - `comunaclic-api.service`
   - `comunaclic-acl.service`
3. Respaldar valores actuales en un vault privado antes de tocar runtime.
4. Confirmar ventana de mantención corta para JWT si se rota signing key.

## Orden recomendado

### 1. Webhook key

- Generar nueva `Payments:InternalWebhookKey`
- Publicar primero el emisor del webhook con soporte al nuevo valor
- Publicar API leyendo el nuevo secreto
- Probar `provider-notify`
- Retirar valor antiguo

### 2. Password reset pepper

- Agregar soporte temporal de doble validación si hay tokens en curso
- Esperar expiración máxima de tokens actuales
- Cambiar `PasswordReset:TokenPepper`

### 3. Refresh token pepper

- Cambiar `Jwt:RefreshTokenPepper`
- Asumir invalidación de refresh tokens existentes
- Mantener access tokens vivos hasta su expiración natural
- Comunicar necesidad de relogin si aplica

### 4. JWT signing key

- Ideal: implementar rotación con key actual + key previa por ventana corta
- Si no hay key rollover:
  - publicar ACL y API con la nueva key casi al mismo tiempo
  - asumir invalidación inmediata de access tokens existentes
  - probar login, refresh y autorización

### 5. Credenciales de base de datos

- Crear usuario nuevo en RDS con permisos mínimos equivalentes
- Actualizar servicios para usar el nuevo usuario
- Reiniciar `api` y `acl`
- Verificar login, lectura, escritura y jobs
- Deshabilitar usuario antiguo solo después de validar

## Validación post rotación

- `PasswordHashing:AllowPlainText` debe quedar `false` salvo una emergencia controlada de compatibilidad.
- `PasswordHashing:AllowLegacySha256` puede quedar `true` solo durante una ventana corta de migración de usuarios antiguos.
- `POST /v1/auth/login`
- `POST /v1/auth/register`
- `POST /v1/auth/refresh`
- `POST /v1/auth/password-reset`
- `GET /v1/public/catalog/categories`
- flujo partner autenticado
- webhook de pagos

## Criterios de salida

- ningún secreto sensible queda en repo ni en `appsettings*.json`
- servicios levantan solo con variables de entorno o secret manager
- login y refresh operan correctamente
- monitoreo sin `401`, `500` ni fallos de conexión a DB fuera de lo esperado
