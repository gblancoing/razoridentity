# Brief para API_Ollama: análisis y tendencias con datos del proyecto

**Objetivo:** Que la IA pueda responder preguntas sobre datos reales del proyecto (eventos, alertas, cierres, sectores, etc.) sin entrenar el modelo, inyectando contexto desde API_Ritweb y Rit_Api.

**Decisión:** Implementar todo en **Api_Ollama** (Opción A). RazorIdentity seguirá llamando a vuestra API; vosotros os encargáis de consultar las fuentes de datos, armar el contexto y llamar a Ollama.

---

## Qué debe hacer Api_Ollama

1. **Configurar clientes** para:
   - **API_Ritweb** (BaseUrl en config): eventos, tipos de evento, sectores, criticidades, etc.
   - **Rit_Api** (BaseUrl en config): si hace falta datos de usuarios, proyectos, centros de costo, etc.

2. **Detectar cuándo la pregunta pide datos del proyecto**, por una de estas vías (elegir la que encaje mejor en vuestra API):
   - **Opción recomendada:** Nuevo endpoint `POST api/Ollama/analyze` (o similar) al que RazorIdentity llama cuando el usuario elige “Consultar datos del proyecto” o un especialista “analytics”.
   - **Alternativa:** Añadir el especialista `analytics` o `ritweb` a `api/Ollama/specialists` y que, cuando se llame a `api/Ollama/specialist` con ese especialista, se ejecute el flujo de “consultar datos + inyectar contexto”.

3. **Flujo cuando corresponda:**
   - Recibir la **pregunta del usuario** (y opcionalmente proyecto/periodo si lo enviara RazorIdentity).
   - **Consultar API_Ritweb** (y Rit_Api si aplica), por ejemplo:
     - `GET api/Eventos` → filtrar por tipo Alerta, por `FechaCierre` (cerrados en mes X), por sector, etc.
     - `GET api/TiposEvento` para saber el Id de “Alerta”.
     - Agregar en memoria: conteos por mes, por sector, por criticidad, alertas abiertas vs cerradas.
   - **Construir un bloque de contexto** en texto, ej.:  
     `Contexto: En enero 2025 se cerraron 12 alertas. Abiertas: 5. Por sector: Minería 8, Construcción 4, ...`
   - **Armar el prompt** enviado a Ollama:  
     `Contexto: [bloque anterior]. Pregunta del usuario: [pregunta]. Responde de forma breve y clara usando solo el contexto.`
   - Llamar a **Ollama** (generate o chat) con ese prompt y devolver la respuesta con el mismo formato que ya usáis (ej. `{ "response": "..." }` o lo que consuma RazorIdentity).

4. **Contrato hacia RazorIdentity:**  
   - Si usáis endpoint nuevo `analyze`: mismo cuerpo que `generate` (p. ej. `{ "prompt": "..." }`) y misma forma de respuesta.  
   - Si usáis especialista: RazorIdentity seguirá llamando a `specialist` con `specialist: "analytics"` (o el nombre que defináis); vosotros en ese caso ejecutáis el flujo de datos + contexto + Ollama.

---

## Fuentes de datos de referencia (API_Ritweb)

- **Eventos:** `GET api/Eventos` → lista de eventos (campos útiles: Id, TipoEventoId, FechaCreacion, FechaCierre, RequiereCierre, SectorId, CriticidadId, TipoAlertaId, ProyectoId, etc.).
- **Tipos de evento:** `GET api/TiposEvento` → para identificar tipo “Alerta” (Nombre = "Alerta") y filtrar.
- **Sectores / Criticidades:** `GET api/Sectores`, `GET api/Criticidades` → para resolver nombres en el contexto.

No hace falta que RazorIdentity envíe credenciales de API_Ritweb: Api_Ollama tendrá su propia config (BaseUrl, y si aplica API key) y hará las llamadas server-side.

---

## Ejemplo de flujo completo

1. Usuario en RazorIdentity escribe: “¿Cuántas alertas se cerraron este mes?”
2. RazorIdentity llama a Api_Ollama: `POST api/Ollama/analyze` con `{ "prompt": "¿Cuántas alertas se cerraron este mes?" }` (o `specialist` con `analytics`).
3. Api_Ollama:
   - Llama a `GET api/Eventos` y `GET api/TiposEvento`.
   - Filtra eventos tipo Alerta con `FechaCierre` en el mes actual, cuenta = N.
   - Construye: `Contexto: Este mes se cerraron N alertas.`
   - Prompt a Ollama: `Contexto: Este mes se cerraron N alertas. Pregunta: ¿Cuántas alertas se cerraron este mes? Responde breve.`
4. Ollama devuelve la respuesta en lenguaje natural.
5. Api_Ollama devuelve esa respuesta a RazorIdentity en el mismo formato que `generate`/`specialist`.
6. RazorIdentity la muestra en el Chat.

---

## Resumen para Cursor (API_Ollama)

- **Objetivo:** Respuestas de IA con datos reales del proyecto (análisis, tendencias, conteos).
- **Dónde:** Todo en Api_Ollama: config de API_Ritweb (y Rit_Api si aplica), detección de “pregunta de análisis”, llamadas a esas APIs, construcción de contexto, llamada a Ollama, respuesta.
- **Opciones de contrato:** (1) Endpoint nuevo `api/Ollama/analyze` con mismo request/response que generate, o (2) especialista `analytics` en el flujo actual de `specialist`.
- **RazorIdentity:** Sin cambios de contrato; solo llamará al endpoint que defináis (generate, specialist con analytics, o analyze) con la pregunta del usuario.

---

## Integración en RazorIdentity (hecho)

- **DTOs:** `SpecialistRequest` con `Month`, `Year`, `ProyectoId`, `Model` opcionales; `AnalyzeRequest` para `api/Ollama/analyze`.
- **Cliente:** `IOllamaApiClient` / `OllamaApiClient`: `SpecialistAsync(..., month, year, proyectoId, model)` y `AnalyzeAsync(prompt, proyectoId, month, year, model)`.
- **Chat:** El desplegable de especialistas incluye **analytics** (viene de Api_Ollama o fallback si la API no responde). Al elegir "Analytics" y enviar un mensaje se llama a `POST api/Ollama/specialist` con `{ "specialist": "analytics", "prompt": "..." }`; la respuesta se muestra en el chat. Opcionalmente se puede usar `POST api/Ollama/analyze` desde código con `_ollama.AnalyzeAsync(prompt, ...)`.
- **UI:** Texto bajo el título del Chat aclara que el especialista "Analytics" responde con datos del proyecto (alertas, eventos, tendencias) si Api_Ollama tiene configurada API_Ritweb.
- **Periodo:** Para el especialista "analytics", RazorIdentity envía automáticamente el **mes y año actual** (UTC) en la petición a Api_Ollama para que pueda filtrar o contextualizar el periodo.

---

## Estado en Api_Ollama (implementado)

- **Config:** Ritweb:BaseUrl en appsettings (Development: `https://localhost:7053`); Ritweb:ApiKey en blanco si la API no exige auth.
- **Month/Year:** El controlador pasa `request.Month`, `request.Year` y `request.ProyectoId` a `BuildContextAsync`. Si no vienen, se usa mes/año actual (UtcNow).
- **Wrapper API_Ritweb:** `RitwebApiClient.GetArrayAsync` soporta respuesta directa `[ ... ]` o wrapper con propiedad `data`, `items` o `results` (case-insensitive).
- **Estado actual primero:** `AnalyticsContextService` construye el contexto empezando por “Estado actual: X alertas abiertas” y “Abiertas por sector: …” (sin depender de month/year). Luego el resumen del mes/año (cerradas en ese periodo). Así se responden bien “¿Cuántas alertas hay abiertas?” y “¿Cuántas se cerraron este mes?”.

---

## Pulir en Api_Ollama: días de atraso de alertas abiertas

**Problema:** Si el usuario pregunta “¿Cuántos días de atraso hay en promedio en las alertas abiertas?” (o similar), el modelo responde “0” porque el contexto actual **no incluye** los días de atraso de cada alerta abierta.

**Datos en API_Ritweb:** En “Ver Eventos”, la columna “Días atraso” se calcula para eventos abiertos que requieren cierre: `(DateTime.UtcNow - FechaCreacion).TotalDays` (o el equivalente). Esos valores (ej. 2, 7, 7, 8, 8) vienen de la lógica de la API o del DTO si la API los expone; si no, se pueden calcular en Api_Ollama a partir de `FechaCreacion` para cada evento abierto.

**Qué añadir en AnalyticsContextService (Api_Ollama):** En el bloque “Estado actual”, además de “X alertas abiertas” y “Abiertas por sector”, incluir:

- **Días de atraso** por cada alerta abierta (o al menos: lista de días de atraso y **promedio**).
- Ejemplo de texto para el contexto:  
  `Días de atraso (desde creación) por alerta abierta: 2, 7, 7, 8, 8. Promedio: 6,4 días.`

Así el modelo podrá responder correctamente a “promedio de días de atraso” y a “días de atraso de las alertas abiertas”. Cálculo sugerido en Api_Ollama para cada evento abierto: `diasAtraso = (int)(DateTime.UtcNow - evento.FechaCreacion).TotalDays` (usar la fecha que exponga el DTO: `FechaCreacion` o similar).

---

## Si siempre responde "No hay datos para el periodo solicitado"

1. **Api_Ollama** debe tener en su `appsettings.json` (o Development) la sección **Ritweb** con **BaseUrl** apuntando a la misma URL que usa RazorIdentity para API_Ritweb (ej. `https://localhost:7053`). Si BaseUrl está vacío, Api_Ollama no llama a API_Ritweb y el contexto queda vacío.
2. **API_Ritweb** debe estar en ejecución y devolver datos en `GET api/Eventos` (y `GET api/TiposEvento`, etc.). Comprobar en "Ver Eventos" de RazorIdentity que haya eventos/alertas.
3. Con el soporte de wrapper en RitwebApiClient y el estado actual en AnalyticsContextService (ya implementados), si BaseUrl está bien configurada y API_Ritweb responde, el Chat debería devolver datos reales. Si sigue sin datos, revisar logs de Api_Ollama al recibir la petición analytics.
