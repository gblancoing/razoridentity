# Diagnóstico Build/Publish Local - Abril 2026

## Resumen

La lentitud percibida en `dotnet build` y `dotnet publish` no era un cuelgue total del repo. El problema reproducible se observó principalmente en proyectos web/Razor y se explicó por una combinación de:

- uso del SDK 10 por defecto al no existir `global.json`
- `node reuse` / build server de MSBuild dejando builds demasiado lentos o con poca visibilidad
- ejecución paralela de builds web que comparten salidas (`ComunaClick.Shared`, `ComunaClick.SharedUI`)
- bloqueos de archivos de static web assets (`*.dswa.cache.json`) cuando dos builds compiten

## Hallazgos concretos

### 1. El repo estaba tomando SDK 10 por defecto

No existía `global.json`, por lo que `dotnet --info` resolvía:

- SDK `10.0.201`

Aunque los proyectos apuntan a `net8.0`.

### 2. `node reuse` empeoraba la sensación de cuelgue

Ejemplo observado:

- `ComunaClick.Admin.csproj`
  - `dotnet build --no-restore -v minimal`
  - tardó ~45s
- el mismo proyecto con:
  - `-m:1 -nr:false -clp:PerformanceSummary`
  - bajó a ~5s

### 3. Los builds paralelos de proyectos web se pisan

Al correr `SharedUI` y `App` en paralelo aparecieron errores como:

- `MSB3026` por archivos en uso
- `MSB4018` en `DefineStaticWebAssets`
- bloqueo sobre:
  - `ComunaClick.Shared.dll`
  - `rjsmrazor.dswa.cache.json`

Conclusión:

- no conviene compilar/publicar en paralelo proyectos que dependan de `ComunaClick.Shared` o `ComunaClick.SharedUI`

### 4. `App` y `publish` sí completan con receta estable

Checks confirmados:

- `dotnet build src/ComunaClick/ComunaClick.App.csproj --no-restore -m:1 -nr:false`
  - completó correctamente
  - tiempo observado ~45s
- `dotnet publish src/ComunaClick/ComunaClick.App.csproj --no-restore -m:1 -nr:false`
  - completó correctamente
- `dotnet publish src/ComunaClick.Admin/ComunaClick.Admin.csproj --no-restore -m:1 -nr:false`
  - completó correctamente

Persisten warnings de nullability en:

- `src/ComunaClick.SharedUI/Services/CategoryVisualService.cs`

Pero no bloquean build/publish.

## Cambios aplicados

### `global.json`

Se agregó pin a:

- SDK `8.0.418`

Objetivo:

- evitar resolver accidentalmente con SDK 10
- alinear toolchain con `net8.0`

### `tmp_deploy_scripts/publish_and_deploy_comunaclic.sh`

Se ajustó para usar:

- `DOTNET_CLI_HOME=$BASE_REPO/.dotnet_home`
- `dotnet publish ... -m:1 -nr:false`

Objetivo:

- desactivar node reuse
- forzar publicación secuencial y más estable
- reducir riesgo de contención de archivos en static web assets

## Receta recomendada desde ahora

### Build local

```bash
DOTNET_CLI_HOME=/ruta/al/repo/.dotnet_home \
dotnet build src/ComunaClick/ComunaClick.App.csproj --no-restore -m:1 -nr:false
```

### Publish local

```bash
DOTNET_CLI_HOME=/ruta/al/repo/.dotnet_home \
dotnet publish src/ComunaClick/ComunaClick.App.csproj -c Release -f net8.0 -m:1 -nr:false
```

### Antes de repetir diagnóstico

```bash
dotnet build-server shutdown
```

## Reglas prácticas

- no correr builds/publish web en paralelo si comparten proyectos referenciados
- preferir `-m:1 -nr:false` para `app`, `acl`, `api`, `admin`, `payments`
- mantener `global.json` apuntando a .NET 8
- si aparece lentitud rara, apagar primero build servers antes de seguir

## Lo que sigue pendiente

Aunque ya existe una receta estable de build/publish, todavía conviene:

- revisar warnings de nullability restantes
- validar `api` y `acl` con la misma receta de publish ajustada
- usar esta configuración en el siguiente deploy real con smoke post-deploy
