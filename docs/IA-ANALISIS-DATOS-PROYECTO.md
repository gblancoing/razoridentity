# IA: análisis, tendencias y respuestas sobre datos del proyecto

## Resumen

**Sí** se puede hacer que la IA entregue análisis, tendencias y respuestas concretas sobre el proyecto usando datos de la base de datos y/o de **Rit_Api** y **API_Ritweb**.

- **No hace falta entrenar el modelo.** La forma habitual es **inyectar contexto (RAG)** o darle **herramientas (function calling)** que consulten BD/APIs y pasen los resultados al modelo.
- **Dónde van los cambios:** sobre todo en **Api_Ollama** (o en un servicio nuevo que orqueste Ollama + BD/APIs): lógica para decidir qué consultar, ejecutar consultas y armar el prompt o las llamadas a herramientas.
- **RazorIdentity:** solo haría falta, si acaso, ampliar el Chat (p. ej. un botón o flujo “Consultar datos del proyecto”) y seguir llamando a la misma API de Ollama; el acceso a BD y APIs queda del lado del backend (Api_Ollama o orquestador).

## Integración actual

- RazorIdentity usa **Ollama** vía **Api_Ollama** (`OllamaApi:BaseUrl` en appsettings).
- Endpoints usados: `api/Ollama/generate`, `api/Ollama/specialist`, `api/Ollama/specialists`, `api/Ollama/models`.
- El Chat ya usa “especialistas” (database, frontend, backend, security).

## ¿Dónde implementarlo? Dos opciones

### Opción A: Ajustar Api_Ollama (recomendado si tienen acceso al código)

**Qué hacer:** Modificar la API de Ollama (el backend que expone `api/Ollama/...`) para que:

1. Tenga configuradas las URLs de **API_Ritweb** y **Rit_Api** (o conexión a BD).
2. Antes de llamar al modelo, detecte si la pregunta pide datos del proyecto (por palabras clave, un “especialista” `analytics`, o un endpoint nuevo tipo `api/Ollama/analyze`).
3. Esa API consulte API_Ritweb/Rit_Api (eventos, cierres, sectores, etc.), arme un bloque de “Contexto: …” con los datos y lo inyecte en el prompt.
4. Llame a Ollama con el prompt enriquecido y devuelva la respuesta a RazorIdentity.

**Ventajas:** Toda la lógica de datos y de IA queda en un solo backend; RazorIdentity solo sigue llamando a la misma API. Escalable si luego añaden más fuentes de datos o function calling.

**Desventaja:** Necesitan poder modificar y desplegar Api_Ollama.

---

### Opción B: Hacerlo en RazorIdentity (sin tocar Api_Ollama)

**Qué hacer:** Dejar Api_Ollama como está y que **RazorIdentity**:

1. En el Chat (o en un flujo “Consultar datos del proyecto”), cuando el usuario pregunte por análisis/tendencias, llame primero a **API_Ritweb** y **Rit_Api** con los clientes que ya tiene (`IApiRitwebClient`, `IRitApiClient`).
2. Obtenga los datos (eventos, agregados, etc.), arme un texto tipo “Contexto: En enero se cerraron X alertas; por sector: …”.
3. Construya el prompt final: `Contexto: [datos]. Pregunta del usuario: [pregunta]. Responde de forma breve.`
4. Envíe ese prompt a Api_Ollama con el endpoint actual (`generate` o `specialist`) y muestre la respuesta en el Chat.

**Ventajas:** No hace falta tocar Api_Ollama; todo se hace en el proyecto RazorIdentity que ya tienen abierto.

**Desventaja:** La lógica de “qué consultar” y “cómo armar el contexto” vive en RazorIdentity; si más adelante otros clientes (móvil, otra web) quieren el mismo análisis, habría que duplicar o extraer esa lógica a un servicio compartido.

---

### Recomendación

- **Si pueden editar y publicar Api_Ollama:** usar **Opción A** (ajustar Api_Ollama) y que RazorIdentity solo llame a un endpoint (actual o uno nuevo tipo `analyze`) con la pregunta; el backend se encarga de consultar datos y de Ollama.
- **Si Api_Ollama es externa o no la pueden cambiar:** usar **Opción B** (todo en RazorIdentity): en el handler del Chat, detectar preguntas de análisis, llamar a API_Ritweb/Rit_Api, armar contexto y enviar el prompt enriquecido al `generate`/`specialist` actual.

**En resumen:** Sí, pueden ajustar su API de Ollama (Opción A) para que ella consulte Rit_Api/API_Ritweb y arme el contexto; o pueden no tocarla y hacer toda la lógica en RazorIdentity (Opción B). La elección depende de si tienen acceso al código de Api_Ollama.

## Siguiente paso sugerido

1. **Definir preguntas concretas** que la IA debe poder responder con datos del proyecto, por ejemplo:
   - “¿Cuántas alertas se cerraron este mes?”
   - “Tendencias de eventos por sector”
   - “Alertas abiertas por criticidad”
2. **En Api_Ollama (o orquestador):**
   - Añadir un flujo (RAG o tools) que:
     - Interprete la intención o reciba un “tipo de consulta”.
     - Consulte **API_Ritweb** (y/o Rit_Api / BD) para obtener los datos necesarios.
     - Inyecte esos datos en el prompt como contexto **o** use function calling y pase el resultado al modelo.
   - Opcional: un “especialista” (ej. `analytics` o `ritweb`) cuyo system prompt indique que responde sobre eventos, indicadores y tendencias usando el contexto inyectado.
3. **En RazorIdentity:** opcionalmente un botón o flujo “Consultar datos del proyecto” que llame al mismo Api_Ollama (mismo endpoint o uno nuevo que ya incluya contexto/tools).

## Ejemplo de flujo (RAG)

1. Usuario escribe: “¿Cuántas alertas se cerraron en enero?”
2. Api_Ollama (o orquestador):
   - Detecta que pide datos de eventos/alertas cerradas por periodo.
   - Llama a API_Ritweb (p. ej. eventos filtrados por tipo Alerta, con `FechaCierre` en enero).
   - Obtiene el conteo (o lista resumida).
3. Arma un prompt: “Contexto: En enero se cerraron X alertas. [Detalle opcional]. Responde al usuario de forma breve y clara.”
4. Ollama genera la respuesta en lenguaje natural.
5. RazorIdentity muestra la respuesta en el Chat (sin cambios en la UI salvo si se añade el flujo “Consultar datos del proyecto”).

---

*Documento de referencia para extender la IA del proyecto con análisis y tendencias sobre datos reales (Rit_Api, API_Ritweb, BD).*
