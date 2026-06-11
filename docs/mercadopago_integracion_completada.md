# Integración Mercado Pago Marketplace — ComunaClic
## Resumen de implementación y puesta en producción

**Fecha:** Junio 2026  
**Estado:** ✅ En producción — `https://app.comunaclic.cl`  
**Modelo:** Split de pagos / Marketplace

---

## ¿Qué se logró?

Un vendedor registrado en ComunaClic puede conectar su cuenta de Mercado Pago desde su panel. Cuando un comprador adquiere un producto en su perfil, el pago se procesa automáticamente a través de Mercado Pago con el siguiente reparto:

- **Mercado Pago** descuenta su comisión operativa (~3.49%)
- **ComunaClic** descuenta el 5% configurado como `marketplace_fee`
- **El vendedor** recibe el resto directamente en su cuenta Mercado Pago

Todo el proceso es automático, sin intervención manual.

---

## Arquitectura del sistema

| Componente | Dominio | Puerto | Servicio |
|---|---|---|---|
| Frontend Blazor | `app.comunaclic.cl` | 5100 | `comunaclic-app.service` |
| API backend | `api.comunaclic.cl` | 5101 | `comunaclic-api.service` |
| Auth (ACL) | `acl.comunaclic.cl` | 5102 | `comunaclic-acl.service` |
| Base de datos | PostgreSQL local | 5432 | — |

---

## Problemas encontrados y soluciones aplicadas

### 1. Tablas del marketplace no se creaban en producción
**Problema:** `DatabaseSchemaBootstrap.cs` no incluía la migración `2026-04-23_marketplace_mercadopago.sql` en su lista de migraciones pendientes.  
**Solución:** Se agregó la entrada al array `PendingChecks`:
```csharp
("core", "sellers", "2026-04-23_marketplace_mercadopago.sql", null),
```
**Archivo:** `src/ComunaClick.Api/Persistence/DatabaseSchemaBootstrap.cs`

### 2. `user_id` de MP llegaba como número, no como string
**Problema:** Al completar el OAuth, MP devuelve `user_id` como `long` (ej: `146478257`), pero el record C# lo declaraba como `string?`, causando un `JsonException` y error 500.  
**Solución:** Cambiar el tipo del campo:
```csharp
// Antes:
[property: JsonPropertyName("user_id")] string? UserId,

// Después:
[property: JsonPropertyName("user_id")] long? UserId,
```
Y ajustar el uso en `MercadoPagoOAuthService.cs`:
```csharp
account.MpUserId = token.UserId?.ToString() ?? user?.Id?.ToString();
```
**Archivos:** `MercadoPagoMarketplaceClient.cs`, `MercadoPagoOAuthService.cs`

### 3. Redirect URI apuntaba al dominio incorrecto
**Problema:** La redirect URI estaba configurada como `https://app.comunaclic.cl/api/mercadopago/oauth/callback`, pero la API reside en `api.comunaclic.cl`. El callback llegaba al frontend Blazor que mostraba "No encontrado".  
**Solución:** Corregir la `RedirectUri` en `appsettings.Production.json` del servidor:
```
https://api.comunaclic.cl/api/mercadopago/oauth/callback
```

### 4. Pantalla "No encontramos un seller activo en la sesión" post-OAuth
**Problema:** Al redirigir de vuelta desde MP, la página Blazor cargaba antes de que `AuthStateService.InitializeAsync()` terminara de leer el JWT del localStorage. `PartnerId` era `null` en ese momento.  
**Solución:** Suscribir `MercadoPago.razor` al evento `AuthState.OnChange` para reintentar la carga cuando el token esté disponible:
```csharp
protected override void OnInitialized()
{
    AuthState.OnChange += HandleAuthChange;
}

private void HandleAuthChange()
{
    if (CurrentSellerId != Guid.Empty && _status is null && !_loading)
        InvokeAsync(LoadAsync);
}

public void Dispose()
{
    AuthState.OnChange -= HandleAuthChange;
}
```
**Archivo:** `src/ComunaClick.SharedUI/Pages/Partner/MercadoPago.razor`

### 5. PKCE activado en el portal MP sin estar implementado en el código
**Problema:** La configuración avanzada de la app MP tenía "¿Usas el flujo con PKCE?" en **Sí**, pero el código no genera `code_challenge`. Esto causaba el error genérico "Tenemos un problema" en la pantalla de autorización MP.  
**Solución:** Cambiar a **No** en el portal de MP Developers.

### 6. `seller_id` inválido en la URL de autorización OAuth
**Problema:** La URL de autorización incluía `seller_id=<UUID interno>`, parámetro que Mercado Pago no reconoce.  
**Solución:** Eliminar ese parámetro del método `BuildOAuthAuthorizationUrl()`.  
**Archivo:** `src/ComunaClick.Api/Modules/Marketplace/MercadoPagoMarketplaceClient.cs`

---

## Configuración del portal Mercado Pago Developers

**App:** ComunaClic Marketplace (`client_id: 3264480134450934`)

| Configuración | Valor |
|---|---|
| Modelo | Checkout Pro → Marketplace / Split de pagos |
| PKCE | No |
| Redirect URI producción | `https://api.comunaclic.cl/api/mercadopago/oauth/callback` |
| Redirect URI desarrollo | `https://app.comunaclic.cl/api/mercadopago/oauth/callback` |
| Webhook URL | `https://app.comunaclic.cl/api/webhooks/mercadopago` |
| Eventos suscritos | `payment` |

---

## Configuración del servidor de producción

Las credenciales viven en `/var/www/comunaclic/api/appsettings.Production.json` en el servidor EC2. Este archivo es preservado automáticamente en cada deploy por el script `deploy_comunaclic.sh` (backup + restore).

> ⚠️ Las credenciales reales **nunca deben incluirse en este documento ni en el repositorio git**.

Estructura de la sección `Marketplace:MercadoPago`:
```json
{
  "Marketplace": {
    "MercadoPago": {
      "ClientId": "<CLIENT_ID>",
      "ClientSecret": "<CLIENT_SECRET>",
      "RedirectUri": "https://api.comunaclic.cl/api/mercadopago/oauth/callback",
      "WebhookSecret": "<WEBHOOK_SECRET>",
      "EncryptionKey": "<BASE64_AES_KEY_32_BYTES>",
      "AppBaseUrl": "https://app.comunaclic.cl",
      "ApiBaseUrl": "https://api.mercadopago.com",
      "OAuthAuthorizeUrl": "https://auth.mercadopago.com/authorization",
      "Currency": "CLP"
    }
  }
}
```

---

## Migración de base de datos ejecutada

**Archivo:** `infra/migrations/2026-04-23_marketplace_mercadopago.sql`

Tablas creadas en el schema `core`:

| Tabla | Descripción |
|---|---|
| `sellers` | Perfil de vendedor vinculado a un Partner |
| `seller_mercadopago_accounts` | Tokens OAuth cifrados del vendedor |
| `seller_fee_configurations` | Comisión configurada por vendedor |
| `payment_fees` | Registro del split por cada pago |
| `payment_status_history` | Historial de estados del pago |
| `webhook_events` | Log idempotente de eventos recibidos |

Columnas adicionales agregadas a tablas existentes:
- `core.orders`: `external_reference`, `gross_amount`, `platform_fee_amount`, `net_amount`, `buyer_email`, `buyer_name`
- `core.payments`: `seller_id`, `transaction_amount`, `mercadopago_payment_id`, `idempotency_key`, etc.

La migración es idempotente (`IF NOT EXISTS` en todas las operaciones).

---

## Comisión configurada

| Tipo | Valor |
|---|---|
| Fijo CLP | 0 |
| Porcentaje | 5% |
| Comisión activa | Sí |

Ejemplo para una venta de $10.000 CLP:

| Destino | Monto |
|---|---|
| Comisión Mercado Pago (~3.49%) | -$349 |
| Comisión ComunaClic (5%) | -$500 → llega a cuenta MP de ComunaClic |
| **Vendedor recibe** | **~$9.151** |

---

## Flujo completo de una venta

```
COMPRADOR
  1. Agrega producto al carrito (localStorage)
  2. Va a Checkout → crea Order en la DB
  3. Frontend llama POST /v1/public/checkout/mercadopago
  
API (api.comunaclic.cl)
  4. BuyerCheckoutPaymentService verifica que el vendedor tiene MP conectado
  5. FeeCalculator calcula el 5% de marketplace_fee
  6. MercadoPagoMarketplaceClient.CreateCheckoutProPreferenceAsync()
     usando el access_token del vendedor (descifrado de DB)
  7. Devuelve checkoutUrl (init_point de MP)

COMPRADOR
  8. Redirigido a Mercado Pago → paga con su cuenta o tarjeta

MERCADO PAGO
  9. Hace el split automático: cobra comisión + transfiere marketplace_fee a ComunaClic
 10. Envía webhook POST /api/webhooks/mercadopago

API
 11. Valida firma HMAC del webhook
 12. Consulta estado real del pago en MP API
 13. Actualiza Payment.Status → "approved"
 14. Actualiza Order.Status → "paid"
 15. Reduce stock del producto

VENDEDOR (app.comunaclic.cl/partner/orders)
 16. Ve la orden como "pagada" con desglose: bruto / comisión / neto
```

---

## Scripts de despliegue utilizados

Todos en `tmp_deploy_scripts/`:

| Script | Uso |
|---|---|
| `Publish-AndDeploy-ComunaClic.ps1 -Targets api` | Despliega el backend API |
| `Publish-AndDeploy-ComunaClic.ps1 -Targets app` | Despliega el frontend Blazor |
| `run_migration_on_prod.sh <archivo.sql>` | Ejecuta una migración SQL en producción |

Comando típico de deploy desde PowerShell:
```powershell
.\tmp_deploy_scripts\Publish-AndDeploy-ComunaClic.ps1 -Targets api -SshKey "tmp_deploy_scripts/ubuntu-doc-alfacloud.pem"
.\tmp_deploy_scripts\Publish-AndDeploy-ComunaClic.ps1 -Targets app -SshKey "tmp_deploy_scripts/ubuntu-doc-alfacloud.pem"
```

---

## Archivos del proyecto modificados en esta integración

| Archivo | Cambio |
|---|---|
| `src/ComunaClick.Api/Persistence/DatabaseSchemaBootstrap.cs` | Registrar migración marketplace |
| `src/ComunaClick.Api/Modules/Marketplace/MercadoPagoMarketplaceClient.cs` | Fix tipo `user_id` (string→long), eliminar `seller_id` de URL OAuth |
| `src/ComunaClick.Api/Modules/Marketplace/MercadoPagoOAuthService.cs` | Fix `.ToString()` en asignación de `MpUserId` |
| `src/ComunaClick.SharedUI/Pages/Partner/MercadoPago.razor` | Suscripción a `AuthState.OnChange` para evitar race condition post-OAuth |
| `.gitignore` | Excluir `appsettings.Development.json` del control de versiones |
| `src/ComunaClick.Api/appsettings.Development.json` | Configuración local con credenciales MP (no versionado) |

---

## Gestión de comisiones (Opción A — control exclusivo de ComunaClic)

La comisión **no es configurable por el vendedor**. Solo ComunaClic la administra directamente en la base de datos.

El panel de comisión fue eliminado de la vista del vendedor (`MercadoPago.razor`). Los vendedores únicamente ven su estado de conexión e historial de pagos.

**Para modificar la comisión globalmente** (vía SSH al servidor):
```sql
UPDATE core.seller_fee_configurations
SET percentage_fee = 5, fixed_fee_amount = 0, is_active = true, updated_at = NOW();
```

**Para un vendedor específico:**
```sql
UPDATE core.seller_fee_configurations
SET percentage_fee = 5, updated_at = NOW()
WHERE seller_id = '<UUID del seller>';
```

**Para nuevos vendedores** que se registren, la comisión se inserta automáticamente al conectar su cuenta MP (con el valor por defecto). Se recomienda crear un trigger o ejecutar el INSERT al momento del onboarding:
```sql
INSERT INTO core.seller_fee_configurations (id, seller_id, fixed_fee_amount, percentage_fee, is_active, created_at, updated_at)
VALUES (gen_random_uuid(), '<seller_id>', 0, 5, true, NOW(), NOW())
ON CONFLICT (seller_id) DO NOTHING;
```

---

## Cómo funciona la vinculación OAuth (identidades distintas)

La conexión entre una cuenta ComunaClic y una cuenta Mercado Pago **no requiere que los emails coincidan**. Es una autorización explícita, no una identidad compartida.

**Ejemplo real de producción:**

| Sistema | Cuenta |
|---|---|
| ComunaClic | `guido.blanco@stantec.com` (empresa Stantec) |
| Mercado Pago | `gblanco.espinoza@gmail.com` (cuenta personal MP) |

Esto es válido y correcto. El flujo fue:
1. Usuario ingresa a ComunaClic como `guido.blanco@stantec.com`
2. Hace clic en "Conectar cuenta" → redirige a `auth.mercadopago.com`
3. El navegador tenía sesión activa como `gblanco.espinoza@gmail.com`
4. Se mostró directamente la pantalla de autorización (sin pedir login)
5. Al hacer clic en "Autorizar", MP entregó un token vinculado a `gblanco.espinoza@gmail.com`
6. ComunaClic almacenó ese token asociado al seller **Stantec**

**Resultado en la base de datos:**
```
Seller: Stantec (guido.blanco@stantec.com)
    └── MP Account: 146478257 (gblanco.espinoza@gmail.com)
```

Los pagos de ventas de **Stantec** llegan a la cuenta MP de `gblanco.espinoza@gmail.com`.

> **Aviso importante para vendedores:** al hacer clic en "Conectar cuenta", el dinero irá a la cuenta de Mercado Pago que tenga activa en el navegador en ese momento. Si hay otra sesión de MP abierta, el dinero iría a esa cuenta. Recomendamos verificar qué cuenta está activa antes de conectar.

---

## Flujo completo de vinculación OAuth según el tipo de vendedor

### Vendedor con cuenta MP existente
1. Clic en "Conectar cuenta" → redirige a `auth.mercadopago.com`
2. Si ya tiene sesión activa en MP → muestra autorización directamente
3. Si no tiene sesión → muestra formulario de login MP (email + contraseña)
4. Selecciona país → Chile → Confirmar
5. Pantalla de permisos → "Autorizar"
6. MP redirige a `api.comunaclic.cl/api/mercadopago/oauth/callback?code=...&state=...`
7. API intercambia el `code` por `access_token` + `refresh_token`
8. Tokens guardados cifrados (AES-GCM 256-bit) en `core.seller_mercadopago_accounts`
9. Panel muestra estado "connected" ✅

### Vendedor sin cuenta MP
1. Clic en "Conectar cuenta" → redirige a `auth.mercadopago.com`
2. En la pantalla de login MP hay un enlace **"Crea tu cuenta"**
3. El vendedor crea su cuenta en mercadopago.cl (requiere RUT chileno y correo)
4. Una vez creada, regresa al flujo de autorización desde el paso 4 anterior
5. Conectado ✅

**Requisitos para que un vendedor pueda cobrar:**

| Requisito | Detalle |
|---|---|
| Cuenta Mercado Pago | Gratuita — se crea en mercadopago.cl |
| RUT chileno | Para verificación de identidad KYC en MP |
| Email verificado | El correo con el que se registraron en MP |
| Cuenta bancaria en MP | Para retirar el dinero desde la billetera MP |

---

## Cómo retira el dinero el vendedor

El dinero de las ventas **no llega automáticamente a la cuenta bancaria** del vendedor. Llega a su **billetera digital de Mercado Pago** y el vendedor debe retirarlo manualmente o configurar retiros automáticos.

### Opción 1 — Retiro manual
1. El vendedor ingresa a mercadopago.cl o la app móvil de MP
2. Va a **"Dinero disponible"**
3. Hace clic en **"Retirar dinero"**
4. Selecciona su cuenta bancaria registrada en MP
5. MP transfiere en 1–2 días hábiles (instantáneo con cuentas RUT/BCI en muchos casos)

### Opción 2 — Retiro automático
En la configuración de su cuenta MP, el vendedor puede activar **"Transferencia automática"** para que el dinero se envíe a su banco cada vez que ingresa un pago, sin intervención manual.

> ComunaClic **no controla** los retiros bancarios del vendedor. Los plazos y comisiones de transferencia los gestiona Mercado Pago directamente según el plan del vendedor.

---

## Verificación en producción

✅ Seller conecta su cuenta MP desde `/partner/mercadopago`  
✅ Estado muestra "connected" con MP User ID, scopes y fecha de expiración  
✅ Token se almacena cifrado con AES-GCM 256-bit en `core.seller_mercadopago_accounts`  
✅ Comisión del 5% configurada y activa  
✅ Checkout Pro crea preferencia usando el access token del vendedor  
✅ `marketplace_fee` aplicado automáticamente en cada transacción  
✅ Webhook recibe notificaciones de MP y actualiza estados  
✅ Vendedor ve detalle de órdenes y pagos en su panel  
