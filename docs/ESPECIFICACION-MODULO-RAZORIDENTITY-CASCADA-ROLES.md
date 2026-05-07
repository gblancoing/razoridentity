# Especificacion funcional de permisos y cascada de roles en RazorIdentity

## Objetivo
Definir, de forma clara y trazable, la logica de autenticacion, autorizacion y resolucion de contexto aplicada por RazorIdentity para controlar el acceso a sus modulos en funcion de:

- identidad del usuario autenticado;
- rol operativo del usuario;
- apps asignadas al usuario;
- proyecto efectivo derivado desde la estructura organizacional del mantenedor RIT.

## Alcance
Esta especificacion aplica a los handlers backend en:
- `Pages/Pmo.cshtml.cs`
- `Pages/RitWeb.cshtml.cs`
- `Pages/RitWebProyectos.cshtml.cs`
- `Pages/App.cshtml.cs`

No define la implementacion interna del mantenedor maestro RIT. Su objetivo es documentar como RazorIdentity consume los datos publicados por RIT API para resolver permisos, alcance funcional y contexto de proyecto.

El apartado **Modelo logico del mantenedor RIT** describe exclusivamente las entidades maestras y de asignacion visibles en el menu de administracion del sistema, distinguiendo entre:

- tablas maestras de negocio;
- tablas de asignacion usuario-dimension;
- relacion con los roles locales de Identity cuando corresponde.

## Dependencias de identidad y autorizacion
### 1) Autenticacion base
- Las paginas protegidas usan `[Authorize]`.
- Si no hay usuario autenticado:
  - se devuelve `Challenge()` o redireccion a login, segun el handler/page.

### 2) Roles usados
- `Super_admin` (y variante `Super_Admin`, ambas aceptadas en codigo).
- `Admin`.
- `usuario` (rol base sin privilegios administrativos; en codigo se infiere por descarte en varios flujos).

### 3) Fuentes externas de autorizacion (RIT API)
Rutas tipicamente consumidas por RazorIdentity para cargar catalogos y asignaciones de negocio:

- `api/Paises`, `api/Regiones`, `api/Proyectos`, `api/CentrosCosto`
- `api/Apps`, `api/UsuariosApp`, `api/UsuariosCentro`
- `api/Disciplinas`, `api/UsuariosDisciplina`
- `api/EmpresasColaboradoras`, `api/UsuariosEmpresaColaboradora`

## Modelo logico del mantenedor RIT

### Criterio de modelado

El menu de administracion distingue dos grupos funcionales: **Administracion** y **Asignaciones**. Esa misma separacion se utiliza en este documento como modelo conceptual de datos.

RazorIdentity **no administra** estas tablas de forma local. Solo las **consume** por HTTP desde RIT API. Por tanto:

- los nombres presentados aqui deben entenderse como **entidades logicas**;
- los nombres fisicos reales en la base del mantenedor pueden variar;
- el contrato tecnico observable desde RazorIdentity es el definido por los DTOs de `Models/Api/`.

### A. Entidades maestras de Administracion

| Opcion de menu | Entidad logica | Jerarquia / FK | Endpoint API (lectura en app) | DTO | Campos expuestos en el DTO |
|----------------|----------------|----------------|--------------------------------|-----|----------------------------|
| Paises | Pais | Raiz geografica | `api/Paises` | `PaisApi` | `Id`, `Nombre` |
| Regiones | Region | `PaisId` → Pais | `api/Regiones` | `RegionApi` | `Id`, `Nombre`, `PaisId` |
| Proyectos | Proyecto | `RegionId` → Region | `api/Proyectos` | `ProyectoApi` | `Id`, `Nombre`, `NumeroContrato?`, `RegionId` |
| Centros de costo | Centro de costo | `ProyectoId` → Proyecto | `api/CentrosCosto` | `CentroCostoApi` | `Id`, `Nombre`, `ProyectoId` |
| Apps | App | — | `api/Apps` | `AppApi` | `Id`, `Nombre`, `DescripcionApp?`, `ImagenApp?` |
| Disciplinas | Disciplina | — | `api/Disciplinas` | `DisciplinaApi` | `Id`, `Nombre` |
| Sostenedor de empresas | Empresa colaboradora | `CentroCostoId` → Centro de costo | `api/EmpresasColaboradoras` | `EmpresaColaboradoraApi` | `Id`, `Nombre`, `CentroCostoId`, `Rut?`, `Direccion?`, `Ubicacion?`, `FechaInicioContrato?`, `FechaTerminoEsperadaContrato?`, `DescripcionServicios?`, `Email?`, `Telefono?` |
| Sostenedor de usuarios | Usuario (negocio) | Aparece como `UserId` en todas las asignaciones; debe coincidir con `AspNetUsers.Id` en RazorIdentity | (no un unico listado maestro en esta especificacion) | — | Identificador string comun |
| Control ACL | Permisos del mantenedor RIT | Implementacion propia del servidor RIT | No modelado como tabla en este documento | — | En RazorIdentity el **rol de aplicacion** operativo se toma de ASP.NET Identity (`AspNetRoles` / `AspNetUserRoles`), p. ej. `Super_admin`, `Admin` |

### Jerarquia estructural de negocio

La cadena geografica y organizacional base es la siguiente:

`Pais -> Region -> Proyecto -> Centro de costo`

Adicionalmente:

- `Empresa colaboradora` depende de `Centro de costo`.
- `App` y `Disciplina` se comportan como catalogos independientes.
- `Usuario` se vincula a estas entidades exclusivamente a traves de tablas de asignacion.

### B. Entidades de Asignacion

Cada registro de asignacion representa una relacion explicita entre un usuario y una dimension funcional del mantenedor. Estas entidades son las que materializan el alcance del usuario dentro del ecosistema RIT.

| Opcion de menu | Entidad logica | Relacion | Endpoint API | DTO | Campos del DTO |
|----------------|----------------|----------|--------------|-----|----------------|
| Usuarios por centro | Usuario–Centro de costo | N:M | `api/UsuariosCentro` | `UsuarioCentroApi` | `Id`, `UserId`, `CentroCostoId` |
| Usuarios por disciplina | Usuario–Disciplina | N:M | `api/UsuariosDisciplina` | `UsuarioDisciplinaApi` | `Id`, `UserId`, `DisciplinaId` |
| Usuarios por empresa | Usuario–Empresa colaboradora | N:M | `api/UsuariosEmpresaColaboradora` | `UsuarioEmpresaColaboradoraApi` | `Id`, `UserId`, `EmpresaColaboradoraId`, `RolId?`, `DisciplinaId?` |
| Usuarios por app | Usuario–App (+ proyecto opcional por fila) | N:M usuario–app; `ProyectoId` opcional define proyecto por defecto para esa app | `api/UsuariosApp` | `UsuarioAppApi` | `Id`, `UserId`, `AppId`, `ProyectoId?` |

### Diagrama logico del mantenedor

```
                    Paises
                      ↑
                   Regiones
                      ↑
                   Proyectos
                      ↑
                 CentrosCosto ←── EmpresasColaboradoras
                      ↑
Usuario (UserId) ────┴── UsuarioCentro
     │                    UsuarioApp ──→ Apps
     │                         └──→ ProyectoId? → Proyectos
     ├── UsuarioDisciplina ──→ Disciplinas
     └── UsuarioEmpresaColaboradora ──→ EmpresasColaboradoras
```

### Interpretacion funcional del modelo

Desde el punto de vista de permisos, el modelo anterior se interpreta asi:

1. El usuario existe y se autentica en RazorIdentity.
2. Su identidad funcional en RIT se referencia mediante `UserId`.
3. Las tablas de asignacion determinan sobre que entidades opera el usuario.
4. La asignacion a app habilita ingreso funcional al modulo.
5. La asignacion a centro, combinada con la jerarquia organizacional, permite derivar el proyecto efectivo cuando no viene informado en la asignacion de app.

---

## Tablas y relaciones relevantes solo para permisos

Esta seccion resume exclusivamente las relaciones que RazorIdentity evalua en tiempo de ejecucion para aceptar o restringir acceso.

### 1) Entidades locales involucradas
- `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`: rol del usuario.
- `UserProfiles`: contexto UI; no sustituye reglas de rol ni RIT.

### 2) Entidades RIT involucradas
Ver **Modelo logico del mantenedor RIT**, especialmente:

- `Apps`
- `UsuariosApp`
- `UsuariosCentro`
- `CentrosCosto`
- `Proyectos`
- `Regiones`
- `Paises`

### 3) Relaciones clave evaluadas por RazorIdentity
Las siguientes relaciones son las que efectivamente condicionan la autorizacion:

1. `AspNetUsers` -> `AspNetUserRoles` -> `AspNetRoles`  
   Determina rol operativo (`Super_admin`, `Admin`, usuario base).

2. `AspNetUsers.Id` -> `UsuariosApp.UserId` + `UsuariosApp.AppId`  
   Determina si el usuario puede entrar a una app concreta.

3. `UsuariosApp.ProyectoId` (si existe) -> `Proyectos.Id`  
   Define proyecto directo autorizado para la app.

4. Si no hay proyecto directo:  
   `AspNetUsers.Id` -> `UsuariosCentro.UserId` -> `CentrosCosto.Id` -> `CentrosCosto.ProyectoId` -> `Proyectos.Id`

5. `Proyectos.RegionId` -> `Regiones.PaisId`  
   Mantiene consistencia de cascada (`Pais -> Region -> Proyecto`), pero no bloquea por si sola en handlers de PMO/RitWeb.

### 4) Diagrama relacional resumido
`AspNetUsers`  
-> `AspNetUserRoles` -> `AspNetRoles`  
-> `UsuariosApp` -> `Apps`  
-> `UsuariosApp.ProyectoId` -> `Proyectos`  
-> (`fallback`) `UsuariosCentro` -> `CentrosCosto` -> `Proyectos`  
-> `Proyectos` -> `Regiones` -> `Paises`

## Regla de cascada para resolver proyecto efectivo
RazorIdentity autoriza las operaciones sensibles por **proyecto efectivo**. La cascada `Pais -> Region -> Proyecto` actua como estructura de referencia, pero la validacion operativa se resuelve sobre `ProyectoId`.

### Orden de resolucion para usuarios no `Super_admin`

1. `UsuariosApp.ProyectoId` para la app actual.
2. Si no existe, fallback por centro:
   - `UsuariosCentro` por `UserId`
   - `CentrosCosto` por `CentroCostoId`
   - usar `CentroCosto.ProyectoId`
3. Si no hay proyecto, el usuario queda sin contexto autorizado para operaciones dependientes de proyecto.

## Matriz de autorizacion por modulo

### PMO (`Pages/Pmo.cshtml.cs`)
#### Reglas de entrada
- Requiere usuario autenticado.
- Obtiene `userId` desde Identity; si no existe, responde error de carga.

#### Reglas de proyecto visible
- `Super_admin`: puede ver todos los proyectos (`api/Proyectos`).
- Resto:
  - proyectos permitidos desde `UsuariosApp` para app PMO (`PMO`, `PYC`, `P & C`, `P&C`);
  - fallback por `UsuariosCentro -> CentrosCosto -> ProyectoId`.

#### Regla de `proyectoId` por query
- Si llega `?proyectoId=`:
  - solo se acepta si esta en `ProyectosDisponibles`.
  - si no, se marca error de autorizacion de proyecto.

#### Regla para endpoints JSON del modulo
Los handlers (`OnGetDatosFinancierosAsync`, `OnGetFactorial*`, `OnPostImportarFinancieroAsync`, etc.) operan sobre el `proyectoId` recibido. La autorizacion fuerte del proyecto se realiza en el flujo de seleccion/contexto de PMO.

### RitWeb (`Pages/RitWeb.cshtml.cs`)
#### Reglas de entrada
- Requiere usuario autenticado.
- Requiere que exista la app `RitWeb` en `api/Apps`.
- Requiere que el usuario tenga dicha app en `api/UsuariosApp`; si no, redireccion con error.

#### Reglas de rol por tabs
- `Super_admin`: acceso total, incluyendo tabs de ajustes.
- `Admin`: acceso intermedio (sin tabs exclusivos de `Super_admin`).
- `usuario`:
  - permitido: dashboard, eventos, kanban.
  - bloqueado: tabs administrativos/registro segun validacion de tab.

#### Contexto de proyecto
- `Super_admin`: puede fijar `proyectoId` por query string.
- Otros roles: usan `ProyectoId` resuelto por cascada (`UsuariosApp` primero, fallback por centro).

#### Filtrado de datos
- Eventos y recursos relacionados se filtran segun rol y contexto (empresa/proyecto), evitando visibilidad global para roles no `Super_admin`.

### RitWeb por proyecto (`Pages/RitWebProyectos.cshtml.cs`)
- Endpoint/page reservado solo para `Super_admin` o `Super_Admin`.
- Si no cumple rol, redirecciona a `/App`.
- Permite seleccionar cualquier proyecto y abrir RitWeb con ese contexto.

### App launcher (`Pages/App.cshtml.cs`)
- Muestra catalogo de apps para usuario autenticado.
- Solo marca capacidades extendidas cuando el usuario es `Super_admin`/`Super_Admin` (ejemplo: acceso a selector global de proyectos de RitWeb).

## Contratos de permiso (resumen operativo)
Para que un usuario opere correctamente en RazorIdentity, deben cumplirse de forma acumulativa las siguientes condiciones:

1. Debe estar autenticado en Identity.
2. Debe tener la app asignada en `UsuariosApp`.
3. Debe tener rol apropiado para la accion.
4. Debe tener `ProyectoId` resoluble (directo o por centro) cuando la operacion es por proyecto.

Si alguna de estas condiciones no se cumple, RazorIdentity restringe el acceso, redirecciona al usuario o responde error segun el flujo de la pagina o handler invocado.

## Casos limite y comportamiento esperado
- Usuario con app asignada pero sin proyecto:
  - entra a la app, pero no puede ejecutar correctamente flujos que exigen proyecto.
- `proyectoId` adulterado en query:
  - se invalida si no pertenece al conjunto autorizado del usuario.
- App inexistente en mantenedor:
  - se bloquea acceso y se informa error operacional.

## Recomendaciones de control
- Mantener sincronizadas asignaciones en `UsuariosApp` y `UsuariosCentro`.
- Evitar usuarios de negocio sin `ProyectoId` resoluble.
- Estandarizar uso de rol `Super_admin` (o `Super_Admin`) para administracion global.
- Auditar periodicamente usuarios con acceso a app pero sin contexto de proyecto.

## Conclusion ejecutiva
El modelo de permisos de RazorIdentity depende de dos planos complementarios:

1. **Identity local**, que define autenticacion y rol operativo.
2. **Mantenedor RIT**, que define alcance funcional mediante catalogos y asignaciones.

La decision final de acceso a un modulo no depende solo del rol, sino de la combinacion entre:

- usuario autenticado;
- app asignada;
- proyecto efectivo resoluble;
- validaciones propias del modulo consultado.
