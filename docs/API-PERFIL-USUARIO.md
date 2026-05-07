# Integración RIT_API – Perfil de usuario (NCARGO, nombre, correo)

> **Estado:** Integración implementada en RazorIdentity. La base URL de RIT_API se configura en `appsettings.json` → `ApiSettings:BaseUrl`.

En esta aplicación, el **nombre completo** ya se muestra y edita en **Datos personales**. Para poder **agregar y usar el cargo (NCARGO)** en el perfil desde RIT_API, la API debe exponer ese dato. Aquí se indica qué debe implementar RIT_API.

---

## Qué debe mejorar RIT_API

Para que en **Datos personales** y **Mi perfil** podamos mostrar y, si se desea, sincronizar el **cargo (NCARGO)** desde la API:

### Opción recomendada: ampliar la Ficha de usuario

El cliente ya llama a:

- **GET** `api/Usuarios/{userId}/Ficha`

Hoy la respuesta solo incluye, por ejemplo, `UserId` y `Turno`. RIT_API debe **agregar** al menos:

| Campo (nombre sugerido en API) | Tipo   | Descripción |
|--------------------------------|--------|-------------|
| **`NCargo`** o **`Cargo`**     | string | Cargo o posición del usuario (ej. "Jefe de proyecto", "Ingeniero de terreno"). |
| `NombreCompleto` (opcional)    | string | Nombre completo (el nombre ya se edita en Datos personales; la API puede ser la fuente maestra). |
| `Email` (opcional)             | string | Correo electrónico. |

Ejemplo de respuesta que debería devolver RIT_API:

```json
{
  "userId": "...",
  "turno": "Día",
  "nCargo": "Ingeniero de terreno",
  "nombreCompleto": "Guido Blanco",
  "email": "gblanco@jej.cl"
}
```

Si en la API el campo se llama **`NCargo`**, el cliente puede mapearlo a `Cargo` en el modelo sin problema.

### Alternativa: endpoint de perfil

Si prefieren no tocar la Ficha, RIT_API puede exponer:

- **GET** `api/Usuarios/{userId}/Perfil`

Con una respuesta que incluya al menos **`nCargo`** (o `cargo`), y opcionalmente `nombreCompleto` y `email`.

---

## En esta aplicación (RazorIdentity)

- **Datos personales:** ya tiene el campo **Cargo** (debajo de Nombre completo). Si el cargo viene de RIT_API, se puede usar para prellenar o sincronizar ese campo.
- **Mi perfil:** ya muestra nombre completo, cargo y correo cuando están en el perfil local.
- Cuando RIT_API exponga **NCARGO** (en Ficha o en Perfil), en este cliente se puede:
  1. Añadir la propiedad al DTO (p. ej. `UsuarioFichaApi.NCargo` o `Cargo`).
  2. Al cargar Datos personales o Mi perfil, llamar a la API y rellenar el campo Cargo con el valor devuelto (y guardarlo en `UserProfiles` si se desea).

---

## Resumen para el equipo RIT_API

| Acción en RIT_API | Detalle |
|-------------------|--------|
| **Exponer NCARGO** | Incluir en **GET** `api/Usuarios/{userId}/Ficha` (o en **GET** `api/Usuarios/{userId}/Perfil`) un campo **`nCargo`** o **`cargo`** (string) con el cargo del usuario. |
| Opcional          | Incluir también `nombreCompleto` y `email` para tener una única fuente de verdad del perfil. |

Con eso, en esta app podremos agregar el cargo en el perfil (Datos personales y Mi perfil) usando el valor que venga de RIT_API.

---

## Implementación en RazorIdentity (realizada)

- **DTOs:** `UsuarioFichaApi` con `UserId`, `Turno`, `NCargo` (`[JsonPropertyName("nCargo")]`), `NombreCompleto`, `Email`. `UsuarioFichaApiUpdateDto` para PUT.
- **Datos personales:** Al cargar se llama a `GET api/Usuarios/{userId}/Ficha` y se prellenan Nombre completo y Cargo (desde API o UserProfiles). Al guardar se persiste en UserProfiles y se llama a `PUT api/Usuarios/{userId}/Ficha` con Cargo, NombreCompleto y Email.
- **Mi perfil:** Al cargar se llama a `GET .../Ficha` y se muestran Turno, Cargo, Nombre completo y Correo (prioridad API cuando exista).
- **RitWeb:** Al cargar la página se obtiene la ficha y se usan NombreCompleto, NCargo y Email para el usuario actual (botón "Usar mis datos").
- **Configuración:** Base URL de RIT_API en `ApiSettings:BaseUrl` (appsettings.json).

### Turnos: CRUD en RIT_API (catálogo 5x2, 7x7, 14x14)

El **catálogo de turnos** (5x2, 7x7, 14x14, etc.) se gestiona en **RIT_API**. En RitWeb → Ajustes → **Turnos** el CRUD llama a RIT_API.

**Qué debe exponer RIT_API:**

| Método | URL | Descripción |
|--------|-----|-------------|
| **GET**  | `api/Turnos`     | Lista de todos los turnos (Id, Nombre, Codigo, Orden, Activo). |
| **GET**  | `api/Turnos/{id}`| Un turno por Id (para editar / activar-desactivar). |
| **POST** | `api/Turnos`     | Crear turno. Cuerpo: `{ "nombre": "5x2", "codigo": "5X2", "orden": 1, "activo": true }`. |
| **PUT**  | `api/Turnos/{id}`| Actualizar turno. Cuerpo: mismo que POST. |
| **DELETE** | `api/Turnos/{id}`| Eliminar turno. |

**Contrato del recurso (ejemplo de ítem):**

```json
{
  "id": 1,
  "nombre": "5x2",
  "codigo": "5X2",
  "orden": 1,
  "activo": true
}
```

**Ejemplos de turnos a crear desde el CRUD:**

| Nombre | Código | Descripción típica |
|--------|--------|--------------------|
| 5x2    | 5X2    | 5 días trabajo, 2 descanso |
| 7x7    | 7X7    | 7 días trabajo, 7 descanso |
| 14x14  | 14X14  | 14 días trabajo, 14 descanso |
| 10x4   | 10X4   | 10 días trabajo, 4 descanso |

- **Turno del usuario:** El turno **asignado** a cada usuario no se guarda en este catálogo, sino en la **ficha del usuario** en RIT_API: `GET/PUT api/Usuarios/{userId}/Ficha`, campo `turno` (string, ej. `"5x2"` o el nombre del turno). En **Mi perfil** se muestra el turno obtenido de esa ficha.

**Resumen para RIT_API – Turnos:**

| Acción | Detalle |
|--------|--------|
| **CRUD catálogo** | Exponer `api/Turnos`: GET (lista), GET `{id}`, POST (crear), PUT `{id}` (actualizar), DELETE `{id}`. Propiedades: id, nombre, codigo, orden, activo. |
| **Ejemplos de nombre** | 5x2, 7x7, 14x14, 10x4, etc. |
| **Ficha del usuario** | Sigue usando el campo `turno` (string) en `api/Usuarios/{userId}/Ficha` para el turno asignado a cada usuario. |
