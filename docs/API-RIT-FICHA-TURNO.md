# RIT_API – Ficha de usuario y campo Turno

RazorIdentity usa la **ficha del usuario** en RIT_API para obtener el **Turno** (y otros datos) y mostrarlo automáticamente al registrar un evento en RitWeb. Si "Turno asignado" aparece como "—" o no se guarda en el evento, suele deberse a que RIT_API no devuelve o no persiste el campo `turno`.

---

## 1. Endpoints afectados

| Método | Ruta | Uso en RazorIdentity |
|--------|------|----------------------|
| **GET** | `api/Usuarios/{userId}/Ficha` | Al cargar RitWeb y al registrar evento: obtiene `turno`, nombre, cargo, email para el usuario que registra. |
| **PUT** | `api/Usuarios/{userId}/Ficha` | En **Datos personales**: el usuario guarda su Turno (y nombre, cargo, etc.); RazorIdentity envía el body y espera que RIT_API persista los datos. |

---

## 2. Contrato del campo Turno

- **GET Ficha (respuesta):** debe incluir la propiedad `turno` (string, nullable). Ejemplo: `"turno": "5x2 (1)"` o `"turno": null` si no tiene asignado.
- **PUT Ficha (body):** RazorIdentity envía `turno` (string, opcional). Si el usuario eligió un turno en Datos personales, se envía el valor (ej. `"5x2 (1)"`); si lo dejó vacío, se puede enviar `null` o no incluir la propiedad. RIT_API debe **persistir** ese valor para que en la siguiente GET Ficha se devuelva.

Si PUT no guarda `turno` en base de datos (o GET no lo devuelve), en RitWeb:
- "Turno asignado" aparecerá como "—" al abrir Registrar evento.
- RazorIdentity intenta rellenar el turno al enviar el formulario: si GET Ficha devuelve `turno`, se envía a API_Ritweb aunque el campo en pantalla estuviera vacío. Si GET tampoco devuelve turno, el evento se guarda sin turno.

---

## 3. Contrato completo de la Ficha (GET y respuesta PUT)

**GET** `api/Usuarios/{userId}/Ficha` — respuesta 200:

```json
{
  "userId": "guid-del-usuario",
  "turno": "5x2 (1)",
  "nCargo": "Ingeniero de terreno",
  "nombreCompleto": "Guido Blanco",
  "email": "gblanco@ejemplo.cl"
}
```

- Cualquier propiedad puede ser `null` si no tiene valor. El campo **`turno`** es el que RitWeb usa para "Turno asignado" al registrar evento.

**PUT** `api/Usuarios/{userId}/Ficha` — body (todos opcionales):

```json
{
  "turno": "5x2 (1)",
  "nCargo": "Ingeniero de terreno",
  "nombreCompleto": "Guido Blanco",
  "email": "gblanco@ejemplo.cl"
}
```

- RIT_API debe persistir cada propiedad enviada (incluido `turno`) en la tabla/entidad de ficha del usuario. Si `turno` viene como `null` o string vacío, guardar `null` en BD.

---

## 4. DTOs en RazorIdentity (referencia)

- **UsuarioFichaApi** (respuesta GET): `UserId`, `Turno`, `NCargo`, `NombreCompleto`, `Email`.
- **UsuarioFichaApiUpdateDto** (body PUT): `Turno`, `NCargo`, `NombreCompleto`, `Email`.

El catálogo de turnos (5x2, 7x7, etc.) viene de **GET api/Turnos** (RIT_API); el valor seleccionado por el usuario se guarda en la Ficha (PUT) y se lee en GET Ficha.

---

## 5. Checklist en RIT_API (si el turno no aparece o no se guarda)

- [ ] **PUT** `api/Usuarios/{userId}/Ficha` recibe la propiedad `turno` en el body y la persiste en la tabla/entidad correspondiente.
- [ ] **GET** `api/Usuarios/{userId}/Ficha` devuelve la propiedad `turno` en el JSON con el valor guardado (o null si no tiene).
- [ ] No hay error 500/404 al llamar GET o PUT Ficha desde RazorIdentity (revisar logs si "Turno asignado" sigue en "—" tras completar Datos personales).

Cuando RIT_API persista y devuelva `turno` correctamente, "Turno asignado" se rellenará en RitWeb y el evento se guardará con ese valor en API_Ritweb.

---

## 6. Implementación actual en RIT_API (verificación)

En RIT_API:

- **Entidad:** `UsuarioFicha` (tabla `UsuariosFicha`) con propiedad `Turno` (string, nullable).
- **PUT Ficha:** el body se deserializa en `UsuarioFichaUpdateDto` (incluye `Turno`); el valor se asigna a `entity.Turno` y se persiste con `SaveChangesAsync`.
- **GET Ficha:** se proyecta `UsuarioFichaDto` con `Turno = f.Turno`, por lo que la respuesta JSON incluye `turno` (camelCase) con el valor guardado o null.

Si aun así el turno no sale o no se guarda en el evento, revisar: que RazorIdentity envíe `turno` en el body del PUT, que la base de datos tenga la columna `Turno` en `UsuariosFicha`, y que no haya errores en logs (404/500) al llamar a GET o PUT Ficha.

---

## 7. Comportamiento configurado en RIT_API (actual)

- **PUT con body null o vacío:** si el cliente no envía body o el binding devuelve `dto == null`, RIT_API hace `dto = new UsuarioFichaUpdateDto()` para no lanzar y tratar como actualización con valores vacíos.
- **Campos null o solo espacios:** para `Turno`, `NCargo`, `NombreCompleto` y `Email`, si el valor es null, vacío o solo espacios se persiste `null` en BD; en caso contrario se guarda el valor recortado (`Trim()`). Ejemplo: `entity.Turno = string.IsNullOrWhiteSpace(dto.Turno) ? null : dto.Turno.Trim();`
- **Verificación:** en RIT_API está documentada la tabla que enlaza cada paso de la guía con el código (PUT/GET, migraciones, pruebas en Swagger y desde RazorIdentity).
