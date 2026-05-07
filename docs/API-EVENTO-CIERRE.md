# Cierre de evento (API_Ritweb)

RazorIdentity permite cerrar un evento de tipo "Alerta" (u otro con **Requiere cierre: Sí**) desde la página del informe del evento. **API_Ritweb** implementa el cierre usando siempre el **Tipo de evento** como referencia para "Requiere cierre".

## Validación "Requiere cierre" en la API

En todos los flujos de cierre la API usa **solo el catálogo Tipo de evento** (`TipoEvento.RequiereCierre`), no el campo del evento:

- **PATCH** `api/Eventos/{id}`: se valida con `evento.TipoEvento?.RequiereCierre == true`.
- **POST** `api/Eventos/{id}/Cierre`: misma validación.
- **PATCH** `api/Eventos/{id}/cerrar`: misma validación.

Si el tipo no requiere cierre o no existe, se devuelve **400** con mensaje: *"Este evento no requiere cierre (según el tipo de evento)."*

**Solución de problemas:** Si la API devuelve 400 "Este evento no requiere cierre" pero en el CRUD el tipo (p. ej. Alerta) tiene "Requiere cierre: Sí", suele deberse a que el **evento se carga sin la relación TipoEvento** en el handler de cierre. Entonces `evento.TipoEvento` es `null` y `TipoEvento?.RequiereCierre` resulta `false`. En la API hay que asegurar una de estas dos cosas al validar el cierre:
- Cargar el evento **con** la navegación: por ejemplo en EF Core usar `.Include(e => e.TipoEvento)` al obtener el evento por id.
- O bien hacer una consulta aparte al catálogo: `var tipo = await _context.TiposEvento.FindAsync(evento.TipoEventoId)` y validar con `tipo?.RequiereCierre == true`.

Además:

- **GET** `api/Eventos` (filtro `requiereCierre`): el filtrado usa el tipo (`x.TipoEvento?.RequiereCierre == requiereCierre`), no el campo del evento.
- **EventoDto.RequiereCierre**: en la respuesta del evento se expone la fuente de verdad del tipo: `TipoEvento?.RequiereCierre ?? evento.RequiereCierre`.

## Opción A (recomendada): PATCH parcial

**Método:** `PATCH`  
**Ruta:** `api/Eventos/{id}`  
**Body (JSON):**
```json
{
  "descripcionCierre": "Motivo del cierre y acciones realizadas.",
  "fechaCierre": "2026-02-25T15:30:00Z"
}
```

- Si no se envía `fechaCierre`, la API usa `DateTime.UtcNow`.
- Si el evento no requiere cierre (según tipo) o ya está cerrado → **400**.
- Respuesta correcta: **200**.

## Opción B: POST en subrecurso Cierre

**Método:** `POST`  
**Ruta:** `api/Eventos/{id}/Cierre`  
**Body (JSON):**
```json
{
  "descripcionCierre": "Motivo del cierre y acciones realizadas."
}
```

- La API asigna `FechaCierre = DateTime.UtcNow` y guarda `DescripcionCierre`.
- Respuesta: **200** y `EventoDto` actualizado.
- Mismas validaciones (requiere cierre desde tipo, no cerrado, descripción obligatoria) → **400** si no se cumple.

## Opción C: PATCH en subrecurso cerrar

**Método:** `PATCH`  
**Ruta:** `api/Eventos/{id}/cerrar`  
- Misma validación desde Tipo de evento; body según implementación en la API.

## Comportamiento en RazorIdentity

1. Se intenta **PATCH** `api/Eventos/{id}` con `descripcionCierre` y `fechaCierre`.
2. Si falla (p. ej. 405 o 400), se intenta **POST** `api/Eventos/{id}/Cierre` con `descripcionCierre`.
3. Tras cerrar correctamente, si el usuario adjuntó foto, se envía **POST** `api/Eventos/{id}/Adjuntos/subir` (`tipoAdjunto=FotoCierre`, `archivo`).

## Foto de cierre (opcional)

- **Método:** `POST`  
- **Ruta:** `api/Eventos/{id}/Adjuntos/subir`  
- **Form-data:** `tipoAdjunto = "FotoCierre"`, `archivo` = fichero imagen  

La API solo acepta `FotoCierre` cuando el evento ya tiene `FechaCierre` (está cerrado). El flujo en RazorIdentity es correcto: primero cierre (PATCH o POST Cierre), después subida de la foto.

## Resumen

| Acción        | Método | Ruta                               | Body / Form |
|---------------|--------|------------------------------------|-------------|
| Cerrar evento | PATCH  | `api/Eventos/{id}`                 | `descripcionCierre`, `fechaCierre` (opcional) |
| **O**         | POST   | `api/Eventos/{id}/Cierre`          | `descripcionCierre` |
| **O**         | PATCH  | `api/Eventos/{id}/cerrar`          | (según API) |
| Foto cierre   | POST   | `api/Eventos/{id}/Adjuntos/subir`  | `tipoAdjunto=FotoCierre`, `archivo` (evento ya cerrado) |

Con estas mejoras, el proceso de cierre queda alineado con las métricas y usa siempre el **Tipo de evento** como referencia para "Requiere cierre".
