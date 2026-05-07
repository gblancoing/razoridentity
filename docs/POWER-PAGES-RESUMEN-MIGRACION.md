# Resumen: migrar RazorIdentity solo a Power Pages

Migrar **solo** a Power Pages significa **dejar de usar la app ASP.NET Core** y **recrear toda la experiencia** dentro de un sitio Power Pages (Dataverse + Liquid + formularios). No existe herramienta de migración automática; es un rediseño y reimplementación.

---

## Qué hay hoy en RazorIdentity (a recrear)

| Área | En RazorIdentity | En Power Pages (equivalente) |
|------|------------------|-----------------------------|
| **Páginas** | Index, App, Chat, RitWeb, RitWebProyectos, EventoPdf, Protegido, Privacy, Error | Páginas web en Power Pages Studio (Liquid, HTML, formularios/listas) |
| **Datos** | PostgreSQL + Entity Framework (Identity, UserProfile, etc.) | Tablas en **Dataverse** (o APIs externas) |
| **Autenticación** | ASP.NET Identity (registro, login, roles) | Autenticación del portal (invitados, Azure AD, registro en el portal) |
| **APIs externas** | RitApi, ApiRitweb, Ollama (C# HttpClient) | Llamadas desde **Power Automate** (HTTP) o desde **Power Pages** vía Web API / Azure Functions |
| **Roles** | Super_admin, Admin, Usuario, Usuario_inicial | Permisos de tabla en Dataverse + roles del portal |

---

## Pasos de una migración “solo Power Pages”

1. **Modelar datos en Dataverse**  
   Crear tablas que reemplacen lo que hoy está en PostgreSQL (usuarios/perfiles, proyectos, eventos, etc.). Los datos existentes se migran con scripts o flujos (Power Automate/export-import).

2. **Configurar el sitio Power Pages**  
   Crear el sitio en Power Platform, configurar autenticación (Azure AD, invitados, etc.) y permisos de tabla para cada rol.

3. **Recrear cada pantalla**  
   - **Inicio / App:** página principal con Liquid + contenido estático o listas de Dataverse.  
   - **Chat (Ollama):** página que llama a una API (Azure Function o backend que encapsule Ollama) vía HTTP desde Power Automate o desde JavaScript en la página.  
   - **RitWeb / RitWebProyectos / EventoPdf:** formularios y listas sobre Dataverse; para datos que vengan de RitApi/ApiRitweb, usar flujos que llamen a esas APIs y escriban/leen en Dataverse, o exponer un backend intermedio y llamarlo desde el portal.  
   - **Protegido:** contenido restringido por permisos del portal y roles.  
   - **Privacy / Error:** páginas estáticas en Liquid.

4. **Lógica de negocio (RitApi, ApiRitweb, Ollama)**  
   - **Opción A:** Mantener un backend mínimo (p. ej. Azure Functions o API en App Service) que llame a RitApi, ApiRitweb y Ollama; Power Pages y Power Automate llaman a ese backend (HTTP).  
   - **Opción B:** Llamar desde Power Automate (conector HTTP) a RitApi/ApiRitweb/Ollama y sincronizar datos con Dataverse; el portal solo usa Dataverse.

5. **Desactivar o retirar RazorIdentity**  
   Cuando el sitio Power Pages esté en producción y los usuarios migrados, se deja de usar la app ASP.NET Core (y opcionalmente la base PostgreSQL).

---

## Ventajas e inconvenientes (migración solo Power Pages)

| Ventaja | Inconveniente |
|--------|----------------|
| Un solo stack: portal + Dataverse + Power Platform | Rehacer todas las pantallas y flujos |
| Sin mantener servidor ni PostgreSQL para la app | RitApi, ApiRitweb y Ollama siguen fuera; hay que orquestarlos vía APIs o Power Automate |
| Escalado y hospedaje gestionados por Microsoft | Lógica compleja en C# hay que reimplementarla (Power Fx, flujos, APIs) |
| Integración nativa con M365 y Power Platform | Migración de datos y de usuarios (identidad) a planificar |

---

## Resumen en una frase

**Migrar solo a Power Pages = dejar RazorIdentity y reconstruir la aplicación como sitio Power Pages (Dataverse + Liquid + formularios + Power Automate/APIs para RitApi, ApiRitweb y Ollama).**

Documentación de referencia: [Power Pages - Microsoft Learn](https://learn.microsoft.com/es-es/power-pages/).
