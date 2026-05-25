# Mejora del flujo de registro — ComunaClic (3 perfiles)

## Rol
Actuá como arquitecto de producto + desarrollador senior en el monorepo ComunaClic (.NET 8, Blazor, ACL + Api). Leé primero:
- docs/capacidades_y_beneficios_comunaclic.md
- docs/pendientes_por_fases_comunaclic.md
- README.md (sección UI pública y registro)
- src/ComunaClick.SharedUI/Pages/RegisterBusiness.razor
- src/ComunaClick.SharedUI/Pages/RegisterBusinessSetup.razor
- src/ComunaClick.SharedUI/Pages/Buyer/Profile.razor
- src/ComunaClick.Acl/Controllers/AuthController.cs (ExternalLogin)

## Objetivo del producto
ComunaClic conecta personas con negocios, servicios y profesionales por comuna. El registro debe ser simple (Google o email) pero **obligar a completar el perfil correcto** según el tipo elegido antes de operar o aparecer en búsqueda pública.

## Perfiles a soportar (paridad funcional)

### 1. Persona natural (comprador)
- **Entrada:** `/register?role=natural` o CTA “Registrar” en home.
- **Barrera de acceso:** Google OAuth O email/contraseña (reCAPTCHA si está activo).
- **Post-registro (wizard):**
  - Nombre, correo (si manual), país/región/comuna, dirección opcional con mapa.
  - Persistir en `Customer` (buyer) y `localStorage` geo para discovery.
- **Destino final:** `/buyer/profile` o `returnUrl`; puede comprar/reservar/contactar sin ser partner.
- **No crea** partner tipo A/B/C.

### 2. Empresa / comercio (partner tipo A)
- **Entrada:** `/register?role=commerce`.
- **Barrera:** Google O manual (misma UX que persona).
- **Post-registro (wizard en pasos):**
  1. Datos del negocio en `/register/business?defaultType=A` (nombre, RUT, categoría, dirección, contacto, geo).
  2. Checklist de activación partner (producto mínimo, visibilidad).
  3. Redirigir a `/my-businesses` o panel partner.
- **Google debe respetar `role=commerce`** y llevar a onboarding A, no a ruta genérica.

### 3. Profesional (partner tipo C + perfil profesional)
- **Entrada:** `/register?role=professional`.
- **Barrera:** Google O manual.
- **Post-registro (wizard en pasos):**
  1. Crear partner tipo C en `/register/business?defaultType=C` (datos mínimos de “negocio profesional”).
  2. Completar **perfil profesional** (especialidad/slug, presentación, fotos, enlaces, términos, geo) vía `BuyerProfessionalProfileController` / tab Profesional en Profile.
  3. Solo visible en `/profesionales/especialidad/{slug}` cuando: `IsActive`, `IsVerified`, partner C visible, geo coherente.
- **Google debe respetar `role=professional`** y `defaultType=C`.

## Requisitos técnicos

1. **Unificar OAuth Google para los 3 roles**
   - Al iniciar Google desde `/register?role=X`, persistir `role` (y `returnUrl`) en `sessionStorage` antes del redirect a Google.
   - En callback (`TryHandleGoogleCallbackAsync`), leer ese intent y redirigir igual que el registro manual:
     - natural → wizard buyer → `/buyer/profile`
     - commerce → `/register/business?defaultType=A`
     - professional → `/register/business?defaultType=C` → wizard perfil profesional
   - Si el usuario ya existe en ACL, iniciar sesión y continuar wizard si perfil incompleto.

2. **Estado “perfil incompleto”**
   - Tras login/registro, si faltan campos obligatorios del tipo elegido, bloquear navegación pública de partner o mostrar banner persistente hasta completar.
   - Endpoints existentes de activación partner y perfil profesional deben usarse como fuente de verdad.

3. **UX del wizard**
   - Barra de progreso (paso 1/3, 2/3, 3/3) según tipo.
   - Copy en español vía `LocaleService` (claves `registerAccount.*` y nuevas `register.wizard.*`).
   - No mostrar LinkedIn/Facebook como activos hasta implementarlos; Google + email son obligatorios.

4. **No romper**
   - Uploads en `/var/www/comunaclic/shared-uploads` (deploy).
   - Quality checks que buscan “Registrar negocio” en login.
   - Favoritos, geo, búsqueda por `specialtySlug`.

## Entregables esperados
- Diseño de flujo (diagrama o lista de pantallas/rutas).
- Cambios en `RegisterBusiness.razor`, `app.js` (Google state), ACL si hace falta campo `registrationIntent` en external login.
- Componente wizard reutilizable o rutas `/register/complete/{step}`.
- Pruebas manuales documentadas: los 3 tipos × (Google + manual).
- Sin commits ni deploy salvo que se pida.

## Criterios de aceptación
- [ ] Profesional con Google termina con partner C + perfil profesional guardado y aparece en búsqueda por especialidad/comuna.
- [ ] Comercio con Google termina en onboarding A con checklist claro.
- [ ] Persona natural con Google termina con customer + comuna en perfil.
- [ ] Cambiar de tipo en `/register` antes de crear cuenta actualiza todo el copy y el destino post-OAuth.

## Flujo implementado (referencia técnica)

| Paso | Ruta | Descripción |
|------|------|-------------|
| Elección tipo | `/register?role=natural\|commerce\|professional` | Selector + formulario |
| OAuth state | `sessionStorage` `comunaclic.registerIntent` | role + returnUrl antes de Google |
| Post OAuth | Igual que manual | Ver `RegistrationFlow.ResolvePostAuthDestination` |
| Wizard natural | `/register/complete/buyer` | Redirige a perfil comprador con banner |
| Wizard commerce | `/register/business?defaultType=A` | Onboarding partner A |
| Wizard professional | `/register/business?defaultType=C` → `/buyer/profile?tab=professional` | Partner C + tab profesional |
