# API Evento – Foro de seguimiento

Especificación para implementar en **API_Ritweb** el foro de registro de seguimiento por evento.

## Objetivo

Un evento (alerta/inspección) puede registrarse un día y cerrarse muchos días después. En ese período se realizan gestiones para dar cierre. El **foro** permite registrar esas gestiones (comentarios, actualizaciones, respuestas) como historial de seguimiento del evento.

## Participantes del foro

Solo pueden publicar y ver el foro los usuarios que pertenezcan a:

- **Misma disciplina** que el usuario que registró el evento.
- **Misma empresa colaboradora** que el usuario que registró el evento.

(La relación usuario ↔ disciplina y usuario ↔ empresa debe existir en RIT/API; el evento ya tiene `UserId`, `EmpresaColaboradoraId` y opcionalmente `DisciplinaId`.)

---

## Tablas sugeridas (API_Ritweb / base de datos)

### 1. `EventoForoPost`

| Columna        | Tipo        | Descripción                          |
|----------------|-------------|--------------------------------------|
| Id             | int (PK)    |                                      |
| EventoId       | int (FK)    | Referencia a Evento                  |
| UserId         | string      | Usuario que escribe (Identity)       |
| FechaCreacion  | datetime    |                                      |
| Contenido      | string/text | Texto del mensaje                    |
| EsPrioridad    | bool        | Actualización prioritaria            |
| ParentId       | int? (FK)   | Null = mensaje raíz; si tiene valor = respuesta a ese post |

Índices: `EventoId`, `ParentId`, `FechaCreacion`.

### 2. Regla de participantes (lógica en API)

Al **crear** un post (POST):

- Obtener el evento y verificar que existe.
- Obtener el usuario actual y su disciplina y empresa (desde RIT API o tablas propias).
- Verificar que el usuario tenga la **misma disciplina** y la **misma empresa** que el `UserId` que registró el evento (o que el evento tenga `EmpresaColaboradoraId` y opcionalmente disciplina asociada).
- Si no cumple, devolver 403.

Al **listar** posts (GET):

- Opcional: filtrar por misma regla (solo participantes autorizados ven el foro). O bien el evento es visible y el foro también para quien vea el evento; la restricción aplica solo al escribir.

---

## Endpoints sugeridos

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET    | `api/Eventos/{eventoId}/Foro` | Lista de posts del foro (raíz y respuestas anidadas o planas). |
| POST   | `api/Eventos/{eventoId}/Foro` | Crear post. Body: `{ "contenido": "...", "esPrioridad": false, "parentId": null }`. Validar participante (misma disciplina + misma empresa). |
| GET    | `api/Eventos/{eventoId}/Foro/Participantes` | (Opcional) Lista de usuarios que pueden participar (misma disciplina y empresa). |

Formato de respuesta típico para GET Foro: array de `EventoForoPostDto` (con `UsuarioNombre`, `UsuarioCargo` resueltos si la API los rellena), ordenados por `FechaCreacion`. Si hay respuestas, pueden venir en `Respuestas` anidado o como lista plana con `ParentId`.

---

## DTO de referencia (RazorIdentity)

Véase `Models/ApiRitweb/EventoForoPostDto.cs`: `Id`, `EventoId`, `UserId`, `UsuarioNombre`, `UsuarioCargo`, `FechaCreacion`, `Contenido`, `EsPrioridad`, `ParentId`, `Respuestas`.

---

## Integración en RazorIdentity (realizada)

- **EventoPdf.cshtml**: panel "Foro de seguimiento" que lista posts (árbol con respuestas) y formulario para nuevo mensaje (contenido + checkbox "Actualización prioritaria").
- **EventoPdf.cshtml.cs**:
  - **GET**: se llama a `GET api/Eventos/{id}/Foro` y se rellenan `ForoPosts` (lista en árbol con `Respuestas` anidadas).
  - **POST NuevoSeguimiento**: antes de enviar se obtiene el evento y desde **RIT API** las asignaciones del usuario actual (`api/UsuariosEmpresaColaboradora`, `api/UsuariosDisciplina`) para enviar `userId`, `empresaColaboradoraId` y `disciplinaId` junto con `contenido`, `esPrioridad` y `parentId`. Si la API devuelve 403, se muestra mensaje: solo participan usuarios de la misma empresa y disciplina que quien registró el evento.
