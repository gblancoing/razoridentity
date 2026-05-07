# Qué revisar en API_Ritweb cuando RazorIdentity devuelve error 500

Cuando RitWeb muestra **"API_Ritweb respondió con error 500"**, el fallo está **dentro del proyecto API_Ritweb**, no en RazorIdentity. La conexión llega; algo falla al procesar la petición.

## 1. Ver el detalle del error

- En la **página RitWeb** debería aparecer **"Detalle desde la API: …"** en el aviso amarillo (RazorIdentity ya muestra el cuerpo de la respuesta).
- En **API_Ritweb**: ventana **Salida** del IDE (depuración) o consola donde corre la API. Ahí verás la excepción real (NullReference, serialización, etc.).

## 2. Endpoints que llama RitWeb al cargar (por orden)

Al abrir RitWeb, se llaman estos GET en secuencia. El 500 suele ser en el **primero que falle**:

| Ruta | Controller/acción típica en API_Ritweb | Qué revisar |
|------|----------------------------------------|-------------|
| `GET api/TiposEvento` | TiposEventoController | DTO, relaciones (Include). |
| `GET api/TiposEventoDetalle` | TiposEventoDetalleController | DTO; no usar Proyecto ni NumeroContrato. |
| `GET api/Sectores` | SectoresController | DTO, BD. |
| `GET api/TiposDocumentoRespaldo` | TiposDocumentoRespaldoController | DTO, BD. |
| `GET api/TiposAlerta` | TiposAlertaController | DTO, BD. |
| `GET api/Criticidades` | CriticidadesController | DTO, BD. |
| `GET api/Eventos` | EventosController | **Muy frecuente**: DTO (incluye `codigoVp`), Include, filtros, null en relaciones. En API_Ritweb el evento tiene `codigoVp` (string, nullable); si la BD no tiene la columna (migración sin aplicar), suele dar 500. |
| `GET api/Eventos/{id}` | EventosController | Igual que lista; solo se usa al editar un evento. |

## 3. Causas habituales de 500 en estos endpoints

- **NullReferenceException**: una propiedad del modelo/DTO no está inicializada o una relación no tiene `Include` y se accede en la serialización.
- **Serialización JSON**: el DTO que devuelve la API no coincide con lo que espera RazorIdentity (nombres en camelCase, propiedades nuevas sin mapear o que no existen en la entidad).
- **Eventos y CodigoVp**: en API_Ritweb el evento tiene campo **CodigoVp** (string, nullable). Para que no falle: entidad y DTO lo tienen, la migración está aplicada en la BD y no se referencia una columna inexistente.
- **Include / relaciones**: en `GET api/Eventos` (lista o por id), si se hace `.Include(e => e.TipoEvento)` (u otras relaciones), que esas entidades no tengan referencias circulares ni propiedades null que rompan el serializador.
- **Migraciones**: la BD que usa API_Ritweb debe tener aplicadas todas las migraciones (incluida la de CodigoVp y EventoForoPost si se usan).

## 4. Cómo localizar el endpoint que falla

1. Con **API_Ritweb en depuración**, abre RitWeb en el navegador y reproduce el error.
2. En la ventana **Salida** (o consola) de API_Ritweb verás la excepción y la pila de llamadas; el primer frame de tu código suele ser el controller/acción que falló (p. ej. `EventosController.GetAll`).
3. Si en RitWeb ves **"Detalle desde la API: …"**, ese texto suele ser el mensaje o el JSON de error (ProblemDetails) que devuelve API_Ritweb; úsalo para ver el tipo de excepción y el mensaje.

## 5. Checklist rápido en API_Ritweb

- [ ] Ventana **Salida** o logs al reproducir el 500: ¿qué excepción y en qué línea?
- [ ] Controlador que aparece en la pila: ¿es TiposEvento, Eventos, Sectores, …?
- [ ] DTOs de ese endpoint: ¿tienen las mismas propiedades que las entidades (incluido `CodigoVp` en eventos)?
- [ ] Uso de `Include`: ¿alguna relación null o referencia circular al serializar?
- [ ] Migraciones aplicadas en la BD que usa API_Ritweb (sobre todo si hay columnas nuevas como CodigoVp).
- [ ] Probar el mismo endpoint en **Swagger** (GET `api/Eventos`, etc.) y ver si también devuelve 500 y el mismo detalle.

Cuando sepas el **mensaje exacto** del "Detalle desde la API" o de la excepción en Salida, con eso se puede acotar a un controller y una línea concreta en API_Ritweb.

---

## Contrato de Eventos (para RazorIdentity)

- **GET** `api/Eventos` y **GET** `api/Eventos/{id}`: devuelven objetos evento con `codigoVp` (string, null si no hay valor).
- **POST** `api/Eventos` y **PUT** `api/Eventos/{id}`: aceptan en el body `codigoVp` (opcional); null o vacío se guarda como null.

RazorIdentity ya cumple este contrato: `EventoDto` tiene `CodigoVp`, envía `codigoVp` en POST/PUT al registrar/editar y muestra N° Código VP en el informe PDF cuando la API lo devuelve.

---

## Error "columna e.CodigoVp no existe" (resuelto)

**Síntoma:** RitWeb muestra 500 y en "Detalle desde la API" aparece `Npgsql.PostgresException: no existe la columna e.CodigoVp`.

**Causa:** La BD de API_Ritweb no tiene la columna `CodigoVp` en la tabla de eventos (migración no aplicada).

**Solución:** En el proyecto **API_Ritweb** aplicar la migración que agrega la columna:

- `dotnet ef migrations list` → debe listar `20260225140000_EventoCodigoVp`.
- `dotnet ef database update` → debe ejecutar `ALTER TABLE "Eventos" ADD "CodigoVp" ...`.

**Si `dotnet ef database update` dice "up to date" pero la columna sigue sin existir:** la clase de la migración (`20260225140000_EventoCodigoVp.cs`) puede no tener los atributos que EF Core usa para detectarla. En API_Ritweb, agregar arriba de la clase:

```csharp
[DbContext(typeof(RitwebDbContext))]
[Migration("20260225140000_EventoCodigoVp")]
```

Tras eso, `migrations list` mostrará la migración como *Pending* y `database update` la aplicará. CodigoVp ya no debería ser causa de 500; si aparece otro 500, revisar "Detalle desde la API" y logs de API_Ritweb.
