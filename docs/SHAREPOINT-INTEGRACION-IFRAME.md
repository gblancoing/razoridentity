# Integrar RazorIdentity en SharePoint (iframe – túnel)

La aplicación sigue ejecutándose en local (o en tu servidor/IIS). SharePoint solo actúa como **contenedor visual**: el usuario entra a SharePoint y ve la app dentro de un iframe.

## 1. Configuración en la app (ya aplicada)

- En **Program.cs** se añadió el encabezado `Content-Security-Policy` con `frame-ancestors` para permitir que tu sitio de SharePoint pueda incrustar la app en un iframe.
- En **appsettings.json** existe la sección `SharePoint:FrameAncestors`.

**Página objetivo:** [caren.aspx](https://codelcochile.sharepoint.com/sites/gdvp/gpr/SitePages/caren.aspx) (GDVP/GPR – Codelco).

En `appsettings.json` está configurado el tenant de Codelco:

```json
"SharePoint": {
  "FrameAncestors": "https://codelcochile.sharepoint.com"
}
```

Si en el futuro necesitas varios orígenes, sepáralos por espacios en `FrameAncestors`.

**URL de la app al depurar:** `https://localhost:7004/` (las demás son APIs: 7006 Ollama, 7053 ApiRitweb, 44337 RitApi).

---

## Qué hacer en SharePoint (pasos en orden)

### Paso A: Autorizar el dominio en SharePoint

SharePoint bloquea iframes de sitios no autorizados. Hay que permitir tu app.

1. Entra al sitio donde está **caren.aspx**:  
   https://codelcochile.sharepoint.com/sites/gdvp/gpr
2. Clic en el **engranaje** (arriba derecha) → **Información del sitio** → **Ver toda la configuración del sitio**.
3. En **Administración del sitio**, abre **Seguridad de campos HTML**.
4. En la lista de dominios permitidos, **agrega**:
   - `https://localhost:7004`  
   (solo el origen, sin barra final; si te pide formato con puerto, usa exactamente eso).
5. Guarda los cambios.

Así SharePoint aceptará cargar contenido de tu app en un iframe.

### Paso B: Insertar el iframe en caren.aspx

1. Abre la página:  
   https://codelcochile.sharepoint.com/sites/gdvp/gpr/SitePages/caren.aspx
2. Ponla en **modo Editar** (por ejemplo, **Editar** arriba a la derecha).
3. En la página, elige dónde quieres que se vea la app y **añade un nuevo bloque/Web Part**.
4. Busca el Web Part **Inserción** o **Embed** (insertar código/embed).
5. En el cuadro donde pide el código, pega **exactamente**:

```html
<iframe src="https://localhost:7004/" width="100%" height="600px" style="border:none;"></iframe>
```

6. Acepta / **Insertar** y luego **Guardar** o **Publicar** la página.

Con eso el “canal” queda creado: al abrir caren.aspx en SharePoint se cargará tu app (https://localhost:7004/) dentro del iframe. **Ten la app en ejecución** (depurando en 7004) cuando abras la página.

## 2. Habilitar el dominio en SharePoint (referencia)

SharePoint Online bloquea por defecto contenido de sitios externos. Hay que autorizar la URL de tu app.

1. Entra a tu **sitio de SharePoint** (ej. gdvp/gpr).
2. **Configuración** (engranaje) → **Información del sitio** → **Ver toda la configuración del sitio**.
3. En **Administración del sitio**, busca **Seguridad de campos HTML**.
4. Añade el dominio de tu app:
   - En local: `https://localhost:7004`
   - Si usas túnel: la URL pública del túnel (ej. `https://xyz-123.devtunnels.ms`).

## 3. Insertar la app en caren.aspx (referencia)

1. Abre **caren.aspx** y ponla en **modo Editar**.
2. Añade un **Web Part** de tipo **Inserción** o **Embed**.
3. Pega el iframe:

```html
<iframe src="https://localhost:7004/" width="100%" height="600px" style="border:none;"></iframe>
```

## 4. Túnel para que otros usuarios accedan (sin estar en tu red)

Si usas `localhost`, solo tú ves la app. Para que **cualquier usuario** (incluso fuera de tu red) la vea desde SharePoint, usa un **túnel** que exponga tu app con una URL pública HTTPS.

La opción de **Microsoft** que encaja con el mismo entorno de SharePoint/Microsoft 365 es **Dev Tunnels**.

---

### 4.1 Dev Tunnels desde Visual Studio (recomendado)

1. Abre el proyecto **RazorIdentity** en **Visual Studio** (17.6 o superior).
2. Inicia sesión en Visual Studio con tu cuenta **Microsoft** (la misma que uses en SharePoint, si quieres).
3. En la barra superior, en el desplegable de **depuración** (donde suele decir "IIS Express" o "RazorIdentity"), haz clic y busca la opción **"Dev Tunnels"** o **"Create A Tunnel"** / **"Crear un túnel"**.
4. En el cuadro de creación del túnel:
   - **Nivel de acceso:** elige **Public** (cualquiera con el enlace) u **Organization** (solo tu organización, si aplica).
   - **Tipo:** **Persistent** si quieres la misma URL cada vez; **Temporary** si prefieres una URL nueva en cada sesión.
   - Asigna un **nombre** al túnel (ej. `razoridentity-sharepoint`).
5. Inicia la depuración. Visual Studio mostrará la **URL pública** del túnel, parecida a:
   ```text
   https://0pbvlk3m-7004.usw2.devtunnels.ms
   ```
6. **Copia esa URL** (será la que uses en SharePoint en lugar de `https://localhost:7004/`).

Si en tu versión de Visual Studio no ves "Dev Tunnels" en el desplegable, usa la **línea de comandos** (sección siguiente).

---

### 4.2 Dev Tunnels desde línea de comandos (CLI)

1. **Instalar** el CLI de Microsoft (una vez):
   ```powershell
   winget install Microsoft.devtunnel
   ```
2. **Iniciar sesión** (cuenta Microsoft o GitHub):
   ```powershell
   devtunnel user login
   ```
3. **Arranca tu app** en Visual Studio en `https://localhost:7004/` (depuración normal).
4. En **otra terminal** (PowerShell), **exponer el puerto 7004**:
   ```powershell
   devtunnel host -p 7004
   ```
5. En la consola aparecerá la **URL pública**, por ejemplo:
   ```text
   https://<tunnel_id>-7004.usw2.devtunnels.ms/
   ```
   Esa es la URL que usarás en SharePoint.

---

### 4.3 Configurar SharePoint para la URL del túnel

1. En SharePoint (sitio gdvp/gpr), ve a **Configuración** → **Información del sitio** → **Ver toda la configuración del sitio** (si está disponible).
2. En **Seguridad de campos HTML**, **añade el dominio del túnel**, por ejemplo:
   - `https://0pbvlk3m-7004.usw2.devtunnels.ms`  
   (o la que te haya dado Dev Tunnels, **sin** barra final).
3. En la página **caren.aspx**, **Editar** el contenido donde tienes el iframe:
   - Sustituye `https://localhost:7004/` por la **URL del túnel**, por ejemplo:
   ```html
   <iframe src="https://0pbvlk3m-7004.usw2.devtunnels.ms/" width="100%" height="600px" style="border:none;"></iframe>
   ```
4. Guarda o publica la página.

Mientras tu PC esté encendida, la app esté en ejecución y el túnel activo, **cualquier persona con el enlace a caren.aspx** (o al iframe) podrá ver la app aunque no esté en tu red.

---

### Resumen

| Dónde              | Qué hacer                                                                 |
|--------------------|---------------------------------------------------------------------------|
| **Tu PC**          | App en ejecución (puerto 7004) + Dev Tunnels (Visual Studio o `devtunnel host -p 7004`) |
| **SharePoint**     | Dominio del túnel en Seguridad de campos HTML + iframe con la URL del túnel |
| **Otros usuarios** | Abren la página de SharePoint (ej. caren.aspx) y ven la app en el iframe |

Flujo: **Usuario (cualquier red) → SharePoint → iframe con URL del túnel → túnel → tu app en tu máquina**.
