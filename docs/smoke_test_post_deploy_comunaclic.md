# Smoke Test Post-Deploy - ComunaClic

## Objetivo

Dejar una verificación rápida y repetible después de cada publicación de `app`, `acl` y `api`, para detectar regresiones visibles sin depender de inspección manual completa.

## Script disponible

Archivo:

`tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh`

Hace verificaciones HTTP públicas y, si se entregan credenciales QA, también valida un flujo autenticado básico.

## Qué valida

### App

- `GET /`
  - responde `200`
  - contiene branding `ComunaClic`
  - devuelve `Content-Security-Policy`
- `GET /login`
  - responde `200`
  - contiene CTA `Cuenta personal`
  - contiene CTA `Registrar negocio`
- `GET /register/business`
  - responde `200`
  - contiene `Registro de Negocio`

### ACL

- `POST /v1/auth/login` con payload inválido
  - responde `400` o `401`
- `POST /v1/auth/register` con payload inválido
  - responde `400` o `409`

La intención no es autenticar en este paso, sino confirmar que el servicio está arriba, enruta y valida payload/antiabuso.

### API

- `GET /v1/public/geo/countries`
- `GET /v1/public/geo/regions?countryId=<id>`
- `GET /v1/public/geo/comunas?regionId=<id>`
- `GET /v1/public/geo/tenant-by-comuna/<comunaId>`
- `POST /v1/public/geo/tenant-by-comuna/<comunaId>`
- `GET /v1/public/catalog/categories`
- `GET /v1/public/catalog/discovery/<categoryCode>`

El script encadena IDs reales (`countryId`, `regionId`, `comunaId`, `categoryCode`) desde las respuestas anteriores para evitar hardcodear datos.

### Flujo autenticado opcional

Si existen `QA_EMAIL` y `QA_PASSWORD`, el script además intenta:

- `POST /v1/auth/login`
- extraer `accessToken`
- `GET /v1/partners/mine` con `Bearer`
- `GET /my-businesses`

Si no hay credenciales QA, estos checks se marcan como `SKIP`.

## Uso rápido

Desde la raíz del repo:

```bash
bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh
```

Con credenciales QA:

```bash
QA_EMAIL=owner@comunaclic.test \
QA_PASSWORD=test123 \
bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh
```

Con URLs custom para staging o local:

```bash
APP_BASE_URL=https://app-staging.comunaclic.cl \
ACL_BASE_URL=https://acl-staging.comunaclic.cl \
API_BASE_URL=https://api-staging.comunaclic.cl \
bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh
```

Si quieres exigir checks autenticados:

```bash
STRICT_AUTH_CHECKS=1 \
QA_EMAIL=... \
QA_PASSWORD=... \
bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh
```

## Variables soportadas

- `APP_BASE_URL`
- `ACL_BASE_URL`
- `API_BASE_URL`
- `QA_EMAIL`
- `QA_PASSWORD`
- `QA_RECAPTCHA_TOKEN`
- `STRICT_AUTH_CHECKS`
- `CURL_BIN`

## Cuándo correrlo

Ejecutar después de cualquier deploy que afecte:

- `app`
- `acl`
- `api`

Recomendado también cuando se cambien:

- configuración productiva
- reglas Nginx
- headers o CSP
- JWT / scopes / login
- geografía pública

## Qué no reemplaza

Este smoke test no reemplaza:

- QA manual buyer/partner completa
- pruebas de abuso reales para rate limiting y reCAPTCHA
- validaciones profundas de publish `dotnet`
- revisión de logs de systemd/Nginx

Sirve como primera barrera rápida post-deploy.
