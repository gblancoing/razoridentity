# Pruebas manuales — registro tres perfiles

Entorno: app local o `https://app.comunaclic.cl` con Google OAuth configurado.

## Persona natural (`/register?role=natural`)

| # | Método | Pasos | Resultado esperado |
|---|--------|-------|-------------------|
| 1 | Manual | Crear cuenta con correo, aceptar términos | Redirige a `/register/complete/buyer` (paso 2/3) |
| 2 | Manual | CTA “Ir a mi perfil” | `/buyer/profile?wizard=1`, banner si falta comuna |
| 3 | Manual | Guardar país/región/comuna | Banner desaparece, paso 3/3 |
| 4 | Google | Mismo flujo desde `/register?role=natural` | Tras OAuth: misma ruta que manual (no `/buyer` genérico) |
| 5 | Google | Cambiar a comercio antes de Google, volver a natural | `sessionStorage` intent = natural, destino comprador |

## Comercio (`/register?role=commerce`)

| # | Método | Pasos | Resultado esperado |
|---|--------|-------|-------------------|
| 1 | Manual | Registro + wizard paso 1 | `/register/business?defaultType=A&wizard=1` |
| 2 | Manual | Crear partner tipo A | `/partner?welcome=...` con checklist |
| 3 | Google | Registro con Google | `defaultType=A` en URL de negocio |
| 4 | Google | Usuario ya existente | Login y continúa onboarding A |

## Profesional (`/register?role=professional`)

| # | Método | Pasos | Resultado esperado |
|---|--------|-------|-------------------|
| 1 | Manual | Registro | `/register/business?defaultType=C&wizard=1` |
| 2 | Manual | Crear partner C | `/buyer/profile?tab=professional&wizard=1` |
| 3 | Manual | Especialidad, activar perfil, geo | Visible en `/profesionales/especialidad/{slug}` con comuna coherente |
| 4 | Google | Registro | Misma secuencia que manual |
| 5 | — | LinkedIn/Facebook | Mensaje “próximamente”, no redirigen |

## No regresión

- Login sigue mostrando “Registrar negocio” donde corresponda (quality check).
- Favoritos y búsqueda por `specialtySlug` sin cambios.
- Uploads no afectados por este cambio.
