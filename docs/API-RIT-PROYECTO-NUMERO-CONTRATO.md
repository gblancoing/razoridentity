# RIT_API – Proyecto: Número de contrato

RazorIdentity (Ajustes → Proyectos) permite asociar un **número de contrato** a cada proyecto. **RIT_API** ya implementa este campo.

**Estado:** Implementado en RIT_API (modelo, DTOs, migración, GET/POST/PUT). RazorIdentity envía y muestra el valor correctamente.

---

## 1. Recurso afectado

- **Entidad:** `Proyecto`
- **Endpoints:** `GET api/Proyectos`, `GET api/Proyectos/{id}`, `POST api/Proyectos`, `PUT api/Proyectos/{id}`

---

## 2. Campo a agregar en RIT_API

| Campo             | Tipo   | Nullable | Descripción                                      |
|------------------|--------|----------|--------------------------------------------------|
| **NumeroContrato** | string | Sí       | Número de contrato asociado al proyecto (ej. "12345", "CT-2026-001"). |

- En **base de datos:** agregar columna `NumeroContrato` (por ejemplo `nvarchar(100)` o `varchar(100)`) en la tabla de proyectos.
- En el **modelo/entidad** del proyecto: propiedad `NumeroContrato` (string, nullable).
- En **DTOs** de respuesta (GET lista y GET por id): incluir `numeroContrato` en el JSON.
- En **creación (POST)** y **actualización (PUT):** aceptar en el body la propiedad `numeroContrato` (string, opcional). Si se envía vacío o null, guardar null en BD.

---

## 3. Contrato de API

### GET api/Proyectos (lista) y GET api/Proyectos/{id}

**Respuesta (ejemplo):**
```json
{
  "id": 1,
  "nombre": "Proyecto Embalse Cerén",
  "numeroContrato": "648758",
  "regionId": 2
}
```

- Si el proyecto no tiene número de contrato, `numeroContrato` puede ser `null` o omitirse.

### POST api/Proyectos (crear)

**Body (ejemplo):**
```json
{
  "nombre": "Nuevo proyecto",
  "regionId": 2,
  "numeroContrato": "CT-2026-001"
}
```

- `numeroContrato` es **opcional**. Si no se envía o se envía como `null`/string vacío, persistir como null.

### PUT api/Proyectos/{id} (actualizar)

**Body (ejemplo):**
```json
{
  "nombre": "Proyecto actualizado",
  "regionId": 2,
  "numeroContrato": "648758"
}
```

- `numeroContrato` es **opcional**. RazorIdentity envía el valor actual (puede ser null o string).

---

## 4. Implementación en RIT_API (ya aplicada)

- **Modelo:** `Proyecto` con propiedad `NumeroContrato` (string, nullable). `AppDbContext`: `HasMaxLength(100)`.
- **DTOs:** `ProyectoDto` y `ProyectoCreateDto` con `NumeroContrato` (opcional).
- **Migración:** `20260225130000_ProyectoNumeroContrato` — columna `NumeroContrato` en tabla Proyectos (varchar 100, nullable). Aplicar con `dotnet ef database update`.
- **Endpoints:** GET lista/id incluyen `numeroContrato`; POST y PUT aceptan `numeroContrato` (opcional). Valor vacío/null se persiste como null.

RazorIdentity (Ajustes → Proyectos) envía y muestra el número de contrato; RIT_API lo persiste y lo devuelve en las consultas.
