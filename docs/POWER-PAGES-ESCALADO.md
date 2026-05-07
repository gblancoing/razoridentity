# Cómo escalar RazorIdentity a Microsoft Power Pages

Power Pages es la evolución de Power Apps Portals: sitios web externos conectados a **Microsoft Dataverse**, con diseño en Power Pages Studio (Liquid, HTML, formularios) y hospedaje gestionado por Microsoft. **No hay migración automática** desde ASP.NET Core; estas son las formas reales de escalar tu proyecto hacia Power Pages.

---

## Diferencia clave: Power Apps vs Power Pages

| | Power Apps | Power Pages |
|---|------------|-------------|
| **Enfoque** | Apps internas (canvas/model-driven) | Sitios web externos (portales públicos o autenticados) |
| **Usuarios** | Empleados con licencia M365 | Invitados, clientes, partners, empleados |
| **Datos** | Dataverse, SharePoint, conectores | Principalmente Dataverse (+ APIs) |
| **Tu caso** | Ver doc [POWER-APPS-OPCIONES.md](POWER-APPS-OPCIONES.md) | Este documento |

---

## Opción A: Mantener RazorIdentity y usarla desde Power Pages (recomendado para escalar sin reescribir)

Tu app ya está preparada para iframe (SharePoint, `frame-ancestors` en `Program.cs`). Puedes **escalar el acceso** haciendo que Power Pages sea el punto de entrada y muestre RazorIdentity.

### A.1 Publicar RazorIdentity en una URL HTTPS

- Desplegar en **Azure App Service** (o cualquier host HTTPS).
- Configurar `SharePoint:FrameAncestors` en `appsettings` con los orígenes que permitan incrustar la app (incluido el dominio de Power Pages si aplica).

### A.2 Usar Power Pages como “hub” que enlaza o incrusta tu app

- **Enlace:** en Power Pages creas una página (o un botón) que abre la URL de RazorIdentity (misma ventana o nueva pestaña). El usuario termina en tu app.
- **Iframe:** si Power Pages permite incrustar iframes en una página (según la plantilla y políticas), usas la URL de RazorIdentity como `src`. La experiencia sigue siendo tu app C#, solo que se ve dentro del portal.

### A.3 Configuración necesaria

- En **Power Platform (Power Pages)**:
  - Si usas iframe, el administrador debe permitir el origen de tu app en la configuración de seguridad del entorno (por ejemplo, Content-Security-Policy / frame-ancestors según corresponda).
- En **RazorIdentity**:
  - Añadir el dominio del sitio de Power Pages a `FrameAncestors` en `appsettings.json` para que el navegador permita que tu app se cargue dentro del iframe.

**Resumen:** RazorIdentity sigue siendo la aplicación principal (PostgreSQL, Identity, RitApi, ApiRitweb, Ollama). Power Pages solo **escala el acceso** (portal único, enlaces, posible iframe).

---

## Opción B: Recrear parte de la experiencia dentro de Power Pages (Dataverse + Liquid)

“Escalar a Power Pages” en el sentido de **tener funcionalidad nativa dentro del portal** implica reconstruir esa parte en Power Pages.

### B.1 Qué implica

- **Datos:** En Power Pages los datos suelen estar en **Dataverse**. Tu proyecto usa **PostgreSQL** y Entity Framework; no se migra automáticamente. Opciones:
  - **Replicar/sincronizar** datos relevantes a Dataverse (por ejemplo con Power Automate o jobs) y usar Power Pages sobre Dataverse.
  - **Mantener PostgreSQL** como fuente de verdad y exponer **APIs** desde RazorIdentity (o un backend aparte); Power Pages consume esas APIs vía **Power Pages Web API**, **Azure Functions**, o **conector HTTP** en flujos.
- **Pantallas:** Las páginas de Razor (Index, App, Chat, RitWeb, etc.) se rehacen en Power Pages con:
  - **Liquid** (plantillas),
  - **Formularios/listas** de Power Pages (basados en tablas de Dataverse),
  - **JavaScript** para lógica en el cliente.
- **Lógica de negocio:** RitApi, ApiRitweb, Ollama hoy están en C#. En un enfoque “todo en Power Pages” tendrías que:
  - Llamar a **APIs externas** (tu backend o Azure Functions) desde Power Pages o Power Automate, o
  - Reimplementar flujos en **Power Automate** y exponer datos/formularios en Power Pages.

### B.2 Pasos prácticos si eliges esta vía

1. **Definir alcance:** qué flujos o pantallas quieres “dentro” de Power Pages (por ejemplo: formulario de solicitudes, listado de proyectos, perfil público).
2. **Datos:**
   - Si usas **Dataverse:** modelar tablas en Dataverse y, si hace falta, flujos que sincronicen desde PostgreSQL o que llamen a tu API para escribir/leer.
   - Si mantienes **PostgreSQL:** exponer endpoints en RazorIdentity (o en una API intermedia) y consumirlos desde Power Pages (Web API, Azure Functions, o Power Automate + HTTP).
3. **Autenticación:** Power Pages soporta invitados, Azure AD, etc. No migras ASP.NET Identity tal cual; los usuarios del portal pueden ser distintos (por ejemplo, invitados con registro en el portal o SSO con Azure AD).
4. **Crear el sitio en Power Pages:** usar Power Pages Studio para páginas, formularios y listas; Liquid/HTML/JS para la experiencia. Documentación oficial: [Power Pages - Microsoft Learn](https://learn.microsoft.com/es-es/power-pages/).

### B.3 Ventajas e inconvenientes

| Ventaja | Inconveniente |
|--------|----------------|
| Portal externo gestionado por Microsoft, escalable | Hay que rediseñar y reimplementar esa parte |
| Integración nativa con Dataverse y Power Platform | PostgreSQL y lógica C# no viven dentro de Power Pages |
| Menos infraestructura propia que mantener | APIs y sincronización con tu backend pueden ser complejas |

---

## Opción C: Enfoque híbrido (escalar acceso + algo nativo en Power Pages)

- **RazorIdentity** sigue siendo el núcleo: identidad, datos en PostgreSQL, RitApi, ApiRitweb, Ollama.
- **Power Pages** se usa para:
  - **Punto de entrada único:** portal donde el usuario elige “Ir a la aplicación” (enlace o iframe a RazorIdentity) o “Formularios públicos” (formularios nativos de Power Pages sobre Dataverse).
  - **Formularios/listas públicos** que no requieran toda la lógica de RazorIdentity (por ejemplo, solicitudes de contacto, listado de proyectos de solo lectura desde Dataverse).
- **SharePoint** puede seguir siendo otro punto de entrada (por ejemplo caren.aspx con iframe a RazorIdentity), o sustituirse por Power Pages como “hub” principal.

Así escalas el **acceso** (un solo portal) y opcionalmente **parte de la experiencia** en Power Pages, sin reescribir toda la app en C#.

---

## Resumen: escalar “a Power Pages”

| Objetivo | Enfoque |
|----------|--------|
| Que más usuarios accedan a la app actual desde un portal Microsoft | **Opción A:** publicar RazorIdentity en HTTPS y enlazarla o incrustarla desde Power Pages (iframe/enlace). |
| Tener funcionalidad nativa dentro del portal (formularios, listas, Dataverse) | **Opción B:** recrear esa parte en Power Pages (Dataverse + Liquid + APIs si sigues usando PostgreSQL). |
| Combinar portal único + app actual + algo nativo en Power Pages | **Opción C:** híbrido (Power Pages como hub + RazorIdentity + formularios/listas en Power Pages donde convenga). |

Para **escalar sin reescribir**, lo más directo es **Opción A** (o C con poco contenido nativo en Power Pages). Si indicas si prefieres solo “enlazar/incrustar” la app actual o también “recrear pantallas en Power Pages”, se pueden bajar a pasos concretos (por ejemplo: configuración de frame-ancestors para el dominio de Power Pages o diseño de una primera página en Power Pages que llame a tu API).
