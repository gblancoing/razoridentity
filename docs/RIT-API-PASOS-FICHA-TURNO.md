# Qué hacer en RIT_API para que el Turno se guarde y se muestre en RazorIdentity

RazorIdentity envía el **turno** al guardar en "Datos personales" (PUT Ficha) y lo vuelve a pedir al cargar la página (GET Ficha). Si no se persiste en RIT_API, al volver a Datos personales o a RitWeb el turno aparece "— Sin asignar —". Sigue estos pasos **en el proyecto RIT_API**.

---

## Paso 1: Base de datos

- La tabla donde se guarda la ficha del usuario (por ejemplo `UsuariosFicha` o `UsuarioFicha`) debe tener una columna para el turno.
- Nombre recomendado: **`Turno`** (o el que use tu entidad).
- Tipo: **string, nullable** (por ejemplo `nvarchar(200)` o `varchar(200)` en SQL Server).
- Si la columna no existe, crea una migración en RIT_API que la agregue y ejecuta `dotnet ef database update`.

---

## Paso 2: Entidad / modelo

- La entidad que mapea a esa tabla debe tener una propiedad:
  - **Nombre:** `Turno`
  - **Tipo:** `string` (nullable), por ejemplo `public string? Turno { get; set; }`
- Así EF Core puede leer y escribir la columna.

---

## Paso 3: DTO para el body del PUT

- El controller que atiende **PUT** `api/Usuarios/{userId}/Ficha` debe recibir un DTO que tenga la propiedad **Turno** (en C#).
- En JSON, RazorIdentity envía **`turno`** (camelCase). Con la serialización por defecto de ASP.NET Core (camelCase), el DTO puede ser:
  - `public string? Turno { get; set; }`  → se enlaza con `turno` del JSON.
- Ese DTO debe incluir también las demás propiedades que RazorIdentity envía: `nCargo`, `nombreCompleto`, `email`, para no perderlas al actualizar.

Ejemplo de DTO para el body del PUT:

```csharp
public class UsuarioFichaUpdateDto
{
    public string? Turno { get; set; }
    public string? NCargo { get; set; }   // o [JsonPropertyName("nCargo")]
    public string? NombreCompleto { get; set; }
    public string? Email { get; set; }
}
```

---

## Paso 4: Lógica del PUT (persistir Turno)

En el action que maneja **PUT** `api/Usuarios/{userId}/Ficha`:

1. Recibir el `userId` de la ruta y el body (tu `UsuarioFichaUpdateDto`).
2. Buscar o crear el registro de ficha para ese usuario (por ejemplo en `UsuariosFicha`).
3. **Asignar explícitamente** el turno (y el resto) al modelo que vas a guardar, por ejemplo:
   - `entity.Turno = dto.Turno?.Trim();` (o `dto.Turno` si prefieres no recortar).
   - Si `dto.Turno` es `null` o vacío, asignar `null`: `entity.Turno = string.IsNullOrWhiteSpace(dto.Turno) ? null : dto.Turno.Trim();`
4. Llamar a **`SaveChangesAsync()`** (o equivalente) para que se persista en la base de datos.

Comprueba que no haya otro código que sobrescriba `entity.Turno` después de esta asignación o que ignore el DTO.

---

## Paso 5: GET Ficha (devolver Turno)

En el action que maneja **GET** `api/Usuarios/{userId}/Ficha`:

1. Leer de la base de datos el registro de ficha del usuario (incluyendo la columna `Turno`).
2. Devolver un DTO (o objeto anónimo) que tenga la propiedad **Turno** (en C#). Con la serialización por defecto, en el JSON saldrá como **`turno`** (camelCase).
   - Ejemplo: `Turno = ficha.Turno` (donde `ficha` es tu entidad).
3. Si no hay ficha, devolver 404 o un objeto con propiedades en `null` según tu contrato; lo importante es que cuando sí hay ficha, **siempre** incluyas `turno` en la respuesta.

Ejemplo de respuesta que RazorIdentity espera:

```json
{
  "userId": "guid-del-usuario",
  "turno": "5x2 (1)",
  "nCargo": "Ingeniero de terreno",
  "nombreCompleto": "Guido Blanco",
  "email": "gblanco@ejemplo.cl"
}
```

---

## Paso 6: Cómo probar en RIT_API

1. **PUT desde Swagger**
   - Abre Swagger en RIT_API (por ejemplo `https://localhost:44337/swagger`).
   - Localiza **PUT** `api/Usuarios/{userId}/Ficha`.
   - Usa un `userId` válido (el mismo con el que inicias sesión en RazorIdentity).
   - Body de ejemplo:
     ```json
     {
       "turno": "5x2 (1)",
       "nCargo": "Ingeniero P&C",
       "nombreCompleto": "Guido Blanco",
       "email": "tu@email.cl"
     }
     ```
   - Ejecuta y verifica que responda 200 (o 204) sin error.

2. **Comprobar en base de datos**
   - Revisa la tabla de ficha (ej. `UsuariosFicha`) y confirma que la fila de ese usuario tiene el valor guardado en la columna `Turno` (por ejemplo `5x2 (1)`).

3. **GET desde Swagger**
   - Llama a **GET** `api/Usuarios/{userId}/Ficha` con el mismo `userId`.
   - La respuesta debe incluir `"turno": "5x2 (1)"` (o el valor que hayas guardado). Si devuelve `"turno": null` y en BD sí hay valor, el GET no está leyendo o mapeando la columna `Turno`.

4. **Probar desde RazorIdentity**
   - En GPR, entra a **Datos personales**, elige un turno (ej. "5x2 (1)"), guarda.
   - Abre otra página y vuelve a **Datos personales**. Debe seguir mostrando "5x2 (1)". Si sigue en "— Sin asignar —", en RIT_API no se está persistiendo o no se está devolviendo `turno` (revisa de nuevo PUT y GET y la columna en BD).

---

## Resumen rápido

| Qué | Dónde en RIT_API |
|-----|-------------------|
| Columna en BD | Tabla de ficha (ej. `UsuariosFicha`) → columna `Turno`, string nullable. |
| Entidad | Propiedad `public string? Turno { get; set; }`. |
| PUT body | DTO con `Turno`; asignar `entity.Turno = dto.Turno` (o trim) y llamar `SaveChangesAsync`. |
| GET respuesta | Incluir `Turno = ficha.Turno` en el DTO/objeto que devuelves (JSON: `turno`). |

Cuando PUT guarde `Turno` y GET devuelva `turno`, RazorIdentity mostrará y mantendrá el turno en Datos personales y en RitWeb (Turno asignado al evento).
