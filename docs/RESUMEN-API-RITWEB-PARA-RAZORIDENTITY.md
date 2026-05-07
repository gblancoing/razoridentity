# Resumen API_Ritweb para RazorIdentity

Referencia para integración de RazorIdentity con API_Ritweb (cierre de evento y foro de seguimiento).

---

## 1. Cierre de evento

RazorIdentity puede cerrar un evento (ej. tipo "Alerta") de dos formas:

| Acción | Método | Ruta | Body / Form |
|--------|--------|------|-------------|
| Cerrar evento | PATCH | `api/Eventos/{id}` | JSON: `descripcionCierre` (obligatorio), `fechaCierre` (opcional, ISO 8601). Si no se envía `fechaCierre`, la API usa la hora del servidor. |
| **O** | POST | `api/Eventos/{id}/Cierre` | JSON: `descripcionCierre` (obligatorio). La API asigna `FechaCierre = UtcNow`. |
| Foto de cierre (después de cerrar) | POST | `api/Eventos/{id}/Adjuntos/subir` | Form-data: `tipoAdjunto=FotoCierre`, `archivo` = fichero. Solo cuando el evento ya está cerrado. |

- **Recomendación:** Intentar primero PATCH `api/Eventos/{id}`; si devuelve 405, usar POST `api/Eventos/{id}/Cierre`.
- **Validación en API:** "Requiere cierre" se decide por el **Tipo de evento** (catálogo), no por un campo del evento. Tipos como Alerta con "Requiere cierre: Sí" permiten el cierre y responden 200 con el EventoDto actualizado.

---

## 2. Foro de seguimiento del evento

| Acción | Método | Ruta | Descripción |
|--------|--------|------|-------------|
| Listar posts | GET | `api/Eventos/{eventoId}/Foro` | Lista de posts (raíz + respuestas anidadas en `Respuestas`). |
| Crear post | POST | `api/Eventos/{eventoId}/Foro` | Body: `contenido`, `esPrioridad`, `parentId` (opcional, null = raíz), `userId`, `empresaColaboradoraId`, `disciplinaId`. Solo pueden publicar usuarios con misma disciplina y misma empresa que el creador del evento; si no, la API devuelve 403. |
| Participantes (opcional) | GET | `api/Eventos/{eventoId}/Foro/Participantes` | Devuelve criterio (empresa y disciplina del evento). |

En **EventoPdf:** cargar el foro con GET Foro; al enviar nuevo seguimiento, POST Foro con el body anterior (RazorIdentity debe enviar `userId`, `empresaColaboradoraId` y `disciplinaId` del usuario actual).

---

## 3. Resumen técnico para Cursor

- **Cierre:** PATCH `api/Eventos/{id}` (body: `descripcionCierre`, `fechaCierre` opcional) o POST `api/Eventos/{id}/Cierre` (body: `descripcionCierre`). Después, si hay foto: POST `api/Eventos/{id}/Adjuntos/subir` con `tipoAdjunto=FotoCierre` y `archivo`.
- **Foro:** GET `api/Eventos/{eventoId}/Foro` para listar; POST `api/Eventos/{eventoId}/Foro` para crear (incluir en el body datos del usuario para la regla de participantes).
- La API resuelve "requiere cierre" desde el **Tipo de evento** (catálogo); no depender del campo `requiereCierre` del evento en RazorIdentity para la lógica de cierre.
