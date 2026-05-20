# Deploy a producción (EC2)

## Dónde va la llave SSH

La llave **no está en GitHub** (por seguridad). El administrador la tenía en esta carpeta **solo en su máquina local**, por ejemplo:

```text
tmp_deploy_scripts/ubuntu-doc-alfacloud.pem
```

(No subir a Git. El CSV `svc-alfacloud-s3_accessKeys.csv` es para S3, no para SSH al EC2.)

En tu PC debes **copiar el archivo `.pem` aquí** (misma carpeta que este README) o apuntar `SSH_KEY` a la ruta donde lo guardes.

Agrega a `.gitignore` local (no subas la llave):

```gitignore
tmp_deploy_scripts/*.pem
tmp_deploy_scripts/*.ppk
tmp_deploy_scripts/deploy.env
```

## Servidor

| Variable | Valor habitual |
|----------|----------------|
| `HOST` | `3.92.248.0` |
| `SSH_USER` | `ubuntu` |
| App en servidor | `/var/www/comunaclic/app` |
| Servicio systemd | `comunaclic-app.service` |

## Publicar solo frontend (`app`)

PowerShell (Windows):

```powershell
$env:SSH_KEY = "C:\Users\guido\Desktop\ComunaClic\ComunaClic_damj3t\ComunaCLick\tmp_deploy_scripts\comunaclic.pem"
cd "C:\Users\guido\Desktop\ComunaClic\ComunaClic_damj3t\ComunaCLick"
.\tmp_deploy_scripts\Publish-AndDeploy-ComunaClic.ps1 -Targets app
```

Git Bash / macOS:

```bash
export SSH_KEY="$(pwd)/tmp_deploy_scripts/comunaclic.pem"
export HOST=3.92.248.0
bash tmp_deploy_scripts/publish_and_deploy_comunaclic.sh app
```

## Si no encuentras el `.pem`

1. Pedir al administrador el archivo original de la instancia EC2 (Key Pair de AWS).
2. O en **AWS Console** → EC2 → instancia `3.92.248.0` → ver qué Key pair usa el nombre (no permite re-descargar; hay que reenviar el archivo guardado al crear la instancia).
3. Alternativa: acceso por **AWS Systems Manager Session Manager** (sin `.pem`) si está habilitado en la cuenta.

## Scripts en esta carpeta

| Archivo | Uso |
|---------|-----|
| `publish_and_deploy_comunaclic.sh` | `dotnet publish` + deploy |
| `deploy_comunaclic.sh` | Solo copia al EC2 (requiere `.publish/` previo) |
| `Publish-AndDeploy-ComunaClic.ps1` | Wrapper Windows |
| `smoke_post_deploy_comunaclic.sh` | Prueba rápida post-deploy |

## Mercado Pago / secretos de app

Las credenciales de Mercado Pago y JWT **no** van en `tmp_deploy_scripts`. Están en variables de entorno del servidor y en `.env.example` del repo (ver `README_MERCADOPAGO_MARKETPLACE.md`).
