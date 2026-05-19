# Release y Rollback Por Servicio - ComunaClic

## Objetivo

Tener una secuencia corta y consistente para publicar solo los servicios afectados, validar rápido y volver atrás si el deploy rompe algo visible.

## Servicios y targets actuales

Según `tmp_deploy_scripts/deploy_comunaclic.sh`, los targets hoy disponibles son:

- `api`
- `acl`
- `app`
- `payments`
- `payments-app`
- `admin`
- `site`

Para Fase 5, el foco mínimo es:

- `api`
- `acl`
- `app`

## Pre-release Checklist

Antes de publicar cualquier servicio:

1. Confirmar qué servicio cambió realmente.
2. Revisar `git status` y no mezclar cambios locales accidentales.
3. Confirmar variables de entorno productivas vigentes.
4. Verificar que no haya rotación pendiente de secretos que choque con el deploy.
5. Confirmar que el dominio afectado responde por HTTPS:
   - `https://app.comunaclic.cl`
   - `https://acl.comunaclic.cl`
   - `https://api.comunaclic.cl`
6. Si hubo cambios de frontend o auth, preparar credenciales QA para el smoke test autenticado.
7. Si el build local está inestable, asumir publish controlado y validar con smoke post-deploy inmediatamente.

## Release Por Servicio

### 1. Publicar solo servicios afectados

Ejemplos:

```bash
BASE_REPO=/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick \
HOST=3.92.248.0 \
SSH_USER=ubuntu \
tmp_deploy_scripts/publish_and_deploy_comunaclic.sh api
```

```bash
BASE_REPO=/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick \
HOST=3.92.248.0 \
SSH_USER=ubuntu \
tmp_deploy_scripts/publish_and_deploy_comunaclic.sh acl app
```

Notas:

- `publish_and_deploy_comunaclic.sh` hace `dotnet publish` local y luego invoca `deploy_comunaclic.sh`.
- `deploy_comunaclic.sh` crea backup remoto automático antes de limpiar el directorio del servicio:
  - patrón: `${remote_dir}.bak.<timestamp>`

### 2. Validar deploy técnico mínimo

Después del deploy:

1. Confirmar que el comando terminó sin error.
2. Si hubo reinicio de servicio, revisar `systemctl status` en el servidor.
3. Si hubo cambios de proxy, revisar Nginx y TLS.

Checks útiles:

```bash
ssh -i /ruta/a/llave.pem ubuntu@HOST "sudo systemctl status comunaclic-api.service --no-pager"
ssh -i /ruta/a/llave.pem ubuntu@HOST "sudo systemctl status comunaclic-acl.service --no-pager"
ssh -i /ruta/a/llave.pem ubuntu@HOST "sudo systemctl status comunaclic-app.service --no-pager"
```

```bash
ssh -i /ruta/a/llave.pem ubuntu@HOST "sudo journalctl -u comunaclic-api.service -n 100 --no-pager"
```

### 3. Ejecutar smoke test post-deploy

```bash
bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh
```

Con credenciales QA:

```bash
QA_EMAIL=owner@comunaclic.test \
QA_PASSWORD=test123 \
bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh
```

## Criterio mínimo de release OK

Se considera release aceptable cuando:

- el publish/deploy termina sin error
- el servicio reinicia correctamente
- el smoke test post-deploy termina en `OK`
- no hay 5xx inmediatos en rutas públicas críticas

## Secuencia exacta de release candidate

### Caso típico: cambios en `app`, `api` y `acl`

1. Apagar build servers locales:

```bash
cd /Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick
DOTNET_CLI_HOME=/Users/alexolave/desarrollos/damj3t-ComunaCLick/.dotnet_home dotnet build-server shutdown
```

2. Validar publish local uno por uno:

```bash
DOTNET_CLI_HOME=/Users/alexolave/desarrollos/damj3t-ComunaCLick/.dotnet_home \
dotnet publish src/ComunaClick.Api/ComunaClick.Api.csproj --no-restore -c Release -f net8.0 -m:1 -nr:false -v minimal
```

```bash
DOTNET_CLI_HOME=/Users/alexolave/desarrollos/damj3t-ComunaCLick/.dotnet_home \
dotnet publish src/ComunaClick.Acl/ComunaClick.Acl.csproj --no-restore -c Release -f net8.0 -m:1 -nr:false -v minimal
```

```bash
DOTNET_CLI_HOME=/Users/alexolave/desarrollos/damj3t-ComunaCLick/.dotnet_home \
dotnet publish src/ComunaClick/ComunaClick.App.csproj --no-restore -c Release -f net8.0 -m:1 -nr:false -v minimal
```

3. Publicar solo servicios afectados:

```bash
BASE_REPO=/Users/alexolave/desarrollos/damj3t-ComunaCLick/damj3t-ComunaCLick \
HOST=3.92.248.0 \
SSH_USER=ubuntu \
DOTNET_CLI_HOME=/Users/alexolave/desarrollos/damj3t-ComunaCLick/.dotnet_home \
/Users/alexolave/desarrollos/damj3t-ComunaCLick/tmp_deploy_scripts/publish_and_deploy_comunaclic.sh api acl app
```

4. Correr smoke post-deploy:

```bash
APP_BASE_URL=https://app.comunaclic.cl \
ACL_BASE_URL=https://acl.comunaclic.cl \
API_BASE_URL=https://api.comunaclic.cl \
bash tmp_deploy_scripts/smoke_post_deploy_comunaclic.sh
```

5. Correr quality checks mínimos:

```bash
QUALITY_APP_BASE_URL=https://app.comunaclic.cl \
QUALITY_ACL_BASE_URL=https://acl.comunaclic.cl \
QUALITY_API_BASE_URL=https://api.comunaclic.cl \
QUALITY_QA_EMAIL=owner@comunaclic.test \
QUALITY_QA_PASSWORD=test123 \
dotnet run --project src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj
```

6. Si todo pasa:

- registrar release en `memory/YYYY-MM-DD.md`
- commit/push si aún no se hizo
- dejar el release candidate promovido

7. Si falla smoke o quality checks:

- no promover
- revisar `systemctl` y `journalctl`
- hacer rollback usando el backup remoto más reciente

## Checklist específico por servicio

### `api`

- `publish_and_deploy_comunaclic.sh api`
- smoke:
  - geo público
  - `tenant-by-comuna`
  - categorías/discovery
- revisar:
  - errores de DB
  - tenant resolution
  - 401/403 inesperados

### `acl`

- `publish_and_deploy_comunaclic.sh acl`
- smoke:
  - login inválido responde
  - register inválido responde
  - login QA opcional entrega token
- revisar:
  - JWT issuer/audience/key
  - reCAPTCHA
  - rate limiting auth

### `app`

- `publish_and_deploy_comunaclic.sh app`
- smoke:
  - `/`
  - `/login`
  - `/register/business`
  - `/my-businesses` opcional con QA
- revisar:
  - headers CSP
  - referencias a `acl.comunaclic.cl` y `api.comunaclic.cl`
  - errores visuales evidentes

## Cuándo hacer rollback

Hacer rollback si ocurre cualquiera de estos casos:

- 5xx sostenidos en rutas críticas
- login roto
- frontend no carga o redirige mal
- geo pública/discovery deja de responder
- Nginx/systemd queda en estado fallido

## Rollback rápido

El script de deploy ya deja backups remotos por carpeta. El rollback manual recomendado es:

1. identificar backup más reciente del servicio afectado
2. detener el servicio
3. restaurar el backup sobre el directorio actual
4. corregir permisos
5. reiniciar el servicio
6. repetir smoke test

Ejemplo conceptual para `api`:

```bash
ssh -i /ruta/a/llave.pem ubuntu@HOST
sudo systemctl stop comunaclic-api.service
sudo rm -rf /var/www/comunaclic/api/*
sudo cp -a /var/www/comunaclic/api.bak.YYYYMMDD-HHMMSS/. /var/www/comunaclic/api/
sudo chown -R svc-comunaclic:svc-comunaclic /var/www/comunaclic/api
sudo systemctl start comunaclic-api.service
```

Repetir el patrón para:

- `/var/www/comunaclic/acl`
- `/var/www/comunaclic/app`

## Post-rollback Checklist

1. confirmar `systemctl status`
2. ejecutar smoke test otra vez
3. revisar `journalctl`
4. documentar qué release se revirtió y por qué
5. no redeployar sin identificar causa raíz

## Registro mínimo recomendado

Por cada release dejar anotado:

- fecha y hora
- commit o rango de commits
- servicios publicados
- resultado del smoke test
- necesidad o no de rollback

Registrar esto en `memory/YYYY-MM-DD.md` y, si cambia el proceso, actualizar este documento.
