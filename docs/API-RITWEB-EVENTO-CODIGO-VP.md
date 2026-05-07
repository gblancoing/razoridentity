# API_Ritweb – Evento: N° Código VP

RazorIdentity permite digitar un **N° Código VP** al registrar o editar un evento. **API_Ritweb** ya implementa este campo.

**Estado:** Implementado en API_Ritweb (entidad, migración, DTOs, GET/POST/PUT). RazorIdentity envía `codigoVp` al crear y actualizar y lo muestra en el informe del evento.

---

## 1. Recurso afectado

- **Entidad:** `Evento`
- **Endpoints:** `GET api/Eventos`, `GET api/Eventos/{id}`, `POST api/Eventos`, `PUT api/Eventos/{id}`

---

## 2. Campo a agregar en API_Ritweb

| Campo         | Tipo   | Nullable | Descripción                                      |
|---------------|--------|----------|--------------------------------------------------|
| **CodigoVp**  | string | Sí       | Número o código VP asociado al evento (ej. "VP-001", "12345"). |

- En **base de datos:** agregar columna `CodigoVp` (por ejemplo `nvarchar(100)` o `varchar(100)`) en la tabla de eventos.
- En el **modelo/entidad** del evento: propiedad `CodigoVp` (string, nullable).
- En **DTOs** de respuesta (GET lista y GET por id): incluir `codigoVp` en el JSON.
- En **creación (POST)** y **actualización (PUT):** aceptar en el body la propiedad `codigoVp` (string, opcional). Si se envía vacío o null, guardar null en BD.

---

## 3. Contrato de API

### GET api/Eventos (lista) y GET api/Eventos/{id}

**Respuesta (ejemplo, fragmento):**
```json
{
  "id": 1,
  "userId": "...",
  "proyectoId": 1,
  "tipoEventoId": 1,
  "codigoVp": "VP-001",
  "descripcion": "...",
  "fechaCreacion": "2026-02-25T12:34:00Z",
  ...
}
```

- Si el evento no tiene código VP, `codigoVp` puede ser `null` o omitirse.

### POST api/Eventos (crear evento)

**Body (ejemplo, fragmento):**
```json
{
  "userId": "...",
  "empresaColaboradoraId": 1,
  "tipoEventoId": 1,
  "proyectoId": 1,
  "sectorId": 1,
  "fechaEvento": "2026-02-25T15:33:00Z",
  "turno": "Día",
  "codigoVp": "VP-001",
  "descripcion": "...",
  ...
}
```

- `codigoVp` es **opcional**. Si no se envía o se envía como `null`/string vacío, persistir como null.

### PUT api/Eventos/{id} (actualizar evento)

**Body (ejemplo, fragmento):**
```json
{
  "userId": "...",
  "empresaColaboradoraId": 1,
  "tipoEventoId": 1,
  "proyectoId": 1,
  "codigoVp": "VP-001",
  "descripcion": "...",
  ...
}
```

- `codigoVp` es **opcional**. RazorIdentity envía el valor actual (puede ser null o string).

---

## 4. Implementación en API_Ritweb (ya aplicada)

- **Entidad:** `Evento` con propiedad `CodigoVp` (string, nullable). `RitwebDbContext`: `HasMaxLength(100)`.
- **Migración:** `20260225140000_EventoCodigoVp` — columna `CodigoVp` en tabla Eventos (varchar 100, nullable). Aplicar con `dotnet ef database update`. La clase de la migración debe tener los atributos `[DbContext(typeof(RitwebDbContext))]` y `[Migration("20260225140000_EventoCodigoVp")]` para que EF la detecte; si faltan, `database update` puede decir "up to date" y la columna no se crea.
- **DTOs:** `EventoDto` (GET) con `CodigoVp`; `EventoCreateDto` (POST) y `EventoUpdateDto` (PUT) con `codigoVp` opcional.
- **Endpoints:** GET lista/id incluyen `codigoVp`; POST y PUT aceptan `codigoVp` (opcional). Null o vacío → se guarda null; si no, valor trimmed.
- **PUT** `api/Eventos/{id}` actualiza el evento con body completo (incluido `codigoVp`); no modifica FechaCierre ni DescripcionCierre (se usan los endpoints de cierre).

RazorIdentity envía `codigoVp` al crear (POST) y al actualizar (PUT) y muestra el valor en el informe del evento (EventoPdf) cuando la API lo devuelve.
