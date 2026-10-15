# Leaderboard Service

<!-- GUÍA: 2–3 líneas. Qué es el servicio y qué resuelve. -->
Servicio HTTP en .NET que mantiene un leaderboard global de puntuaciones, agregadas por usuario sobre una ventana de tiempo configurable, con actualización near-real-time e ingesta concurrente segura.

> Razonamiento, decisiones y escalado: [SOLUTION.md](SOLUTION.md) · Arquitectura: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)

---

## Tiempo empleado

<!-- GUÍA (OBLIGATORIO): total real + desglose. Si superaste 6 h, dilo y remite a SOLUTION.md. -->

| Fase | Tiempo |
|---|---|
| Diseño y decisiones | _TBD_ |
| Implementación | _TBD_ |
| Tests | _TBD_ |
| Documentación | _TBD_ |
| **Total** | **_TBD_** |

---

## Prerrequisitos

- [.NET SDK 10.0.401](https://dotnet.microsoft.com/download) o superior (fijado en `global.json`)
- **o** Docker 24+ con Docker Compose (no requiere el SDK)

---

## Ejecución rápida

### Opción A: .NET local

```bash
dotnet run --project src/Leaderboard.Api
```

### Opción B: Docker

```bash
docker compose -f deploy/docker-compose.yml up --build -d
```

El contenedor arranca en modo `Production`, en `http://localhost:8080`, con las keys de demostración `demo-writer-key` (escritura y lectura) y `demo-reader-key` (solo lectura). Se cambian con las variables `LEADERBOARD_WRITER_KEY` y `LEADERBOARD_READER_KEY`. Los datos persisten en el volumen `leaderboard-data`. La imagen es chiseled (sin shell) y corre como usuario no-root (UID 1654). Para detenerlo:

```bash
docker compose -f deploy/docker-compose.yml down
```

La opción A arranca en modo `Development` con dos API keys de prueba (definidas en `src/Leaderboard.Api/appsettings.Development.json`):

| API key | Permisos |
|---|---|
| `dev-ingestor-key` | `scores:write`, `scores:read` |
| `dev-reader-key` | `scores:read` |

La base de datos SQLite se crea sola al arrancar en `src/Leaderboard.Api/data/leaderboard.db`.

| Recurso | URL | Auth |
|---|---|---|
| API | `http://localhost:8080` | `X-Api-Key` |
| OpenAPI | `http://localhost:8080/openapi/v1.json` | pública |
| Liveness / readiness | `http://localhost:8080/health/live`, `/health/ready` | pública |
| Métricas (Prometheus) | `http://localhost:8080/metrics` | pública (en producción: solo red interna) |

---

## Configuración

Todas las opciones se validan al arrancar: una configuración inválida impide que el servicio inicie. Por variable de entorno se usa `__` como separador de sección.

| Variable | Descripción | Default |
|---|---|---|
| `ConnectionStrings__Leaderboard` | Cadena de conexión SQLite | `Data Source=data/leaderboard.db` |
| `Leaderboard__Window` | Ventana de agregación (múltiplo de `BucketSize`) | `7.00:00:00` |
| `Leaderboard__BucketSize` | Tamaño del bucket temporal (divisor de un día) | `00:01:00` |
| `Leaderboard__FutureSkewTolerance` | Tolerancia para timestamps futuros | `00:05:00` |
| `Leaderboard__MaxPointsPerEvent` | Puntos máximos por evento | `1000000` |
| `Leaderboard__DefaultTop` | `top` por defecto | `10` |
| `Leaderboard__MaxTop` | Máximo permitido para `top` | `100` |
| `Leaderboard__CacheTtl` | Obsolescencia máxima de `GET /leaderboard` (`0` desactiva la caché) | `00:00:01` |
| `Auth__ApiKeys__0__Name` / `__Key` / `__Roles__0` | API keys y sus roles (`scores:write`, `scores:read`). Sin keys, todo responde `401` | _ninguna fuera de Development_ |
| `RateLimit__PermitsPerSecond` | Peticiones sostenidas por segundo por API key (o por IP sin key) | `500` |
| `RateLimit__Burst` | Tamaño de ráfaga del token bucket | `1000` |
| `Telemetry__OtlpEndpoint` | Endpoint OTLP para exportar trazas (opcional) | _vacío_ |

---

## Uso de la API

Respuestas reales, copiadas de una ejecución local. Todo el JSON usa `snake_case` y los errores siguen RFC 9457 (`application/problem+json`).

### Registrar puntuación: `POST /score`

```bash
curl -i -X POST http://localhost:8080/score -H "Content-Type: application/json" -H "X-Api-Key: dev-ingestor-key" -H "Idempotency-Key: smoke-1" -d "{\"user_id\":\"alice\",\"score\":50,\"timestamp\":\"2026-10-15T19:16:00Z\"}"
```

```
HTTP/1.1 201 Created
Location: /user/alice/score
X-Correlation-Id: 62cf6098ed89ca0e466d9f7efb010659

{"event_id":"k:smoke-1","status":"created"}
```

Un reintento con el mismo `Idempotency-Key` devuelve `200 {"event_id":"k:smoke-1","status":"duplicate"}` y no vuelve a sumar. Sin `Idempotency-Key`, la identidad del evento es un hash del payload (`h:…`).

Payload inválido (devuelve **todos** los errores a la vez, con códigos estables):

```json
{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,
 "errors":{"user_id":["user_id must be 1-64 characters from [A-Za-z0-9_.-]."],"score":["score must be between 0 and 1000000."],"timestamp":["timestamp cannot be more than 00:05:00 ahead of server time."]},
 "error_codes":["user_id.invalid","score.out_of_range","timestamp.in_future"],"trace_id":"bf7964d048d04cb0435076aff0e14e92"}
```

### Top N: `GET /leaderboard?top=N`

```bash
curl "http://localhost:8080/leaderboard?top=5" -H "X-Api-Key: dev-reader-key"
```

```json
{"window_start":"2026-10-08T19:17:00+00:00","window_end":"2026-10-15T19:17:00+00:00","generated_at":"2026-10-15T19:16:14.5407379+00:00",
 "entries":[{"rank":1,"user_id":"bob","score":80},{"rank":2,"user_id":"alice","score":50}]}
```

`generated_at` es el momento en que se leyeron los datos. Si la respuesta viene de la caché, puede ser hasta `CacheTtl` anterior a la petición.

### Score de un usuario: `GET /user/{id}/score`

```bash
curl http://localhost:8080/user/alice/score -H "X-Api-Key: dev-reader-key"
```

```json
{"user_id":"alice","window_score":50,"all_time_score":50,"window_start":"2026-10-08T19:17:00+00:00","window_end":"2026-10-15T19:17:00+00:00"}
```

Un usuario que nunca envió puntuaciones devuelve `404` (`error_codes: ["user.not_found"]`).

### Códigos de estado

| Código | Cuándo |
|---|---|
| `201` / `200` | Puntuación creada / duplicado idempotente |
| `400` | Validación (campos, `top`, `Idempotency-Key`, JSON mal formado) |
| `401` | Falta `X-Api-Key` o no es válida |
| `403` | La key no tiene el rol requerido (p. ej. una key de lectura haciendo `POST`) |
| `404` | Usuario sin puntuaciones |
| `429` | Rate limit superado (incluye `Retry-After`) |

---

## Tests

```bash
dotnet test
```

Por tipo:

```bash
dotnet test tests/Leaderboard.UnitTests
```

```bash
dotnet test tests/Leaderboard.IntegrationTests
```

```bash
dotnet test tests/Leaderboard.E2ETests
```

| Suite | Tests | Cubre |
|---|---|---|
| UnitTests | 84 | Dominio (ventana, buckets, idempotencia, ranking) y casos de uso con reloj falso |
| IntegrationTests | 39 | SQLite real: concurrencia (1.000 escrituras al mismo usuario, 500 reintentos idénticos, lecturas sin estados intermedios) y equivalencia SQL vs dominio. API HTTP en memoria: contratos, validación, 401/403/429, caché, health, métricas, OpenAPI |
| E2ETests | 5 | Servicio real por HTTP real: recorrido completo, límites de seguridad, simulación de carga (5.000 escrituras, 30 % a un hot user, 5 % reintentos) con verificación exacta de todos los totales, lecturas durante escrituras y tormenta de 200 reintentos idénticos. Mide p50/p95/p99 |

Salida esperada: `Correctas! - Con error: 0` en las tres suites (`Superado: 84`, `39` y `5`). Las unitarias y de integración tardan unos 15 s; las E2E, entre 20 y 70 s según el disco.

### Tests E2E

Por defecto, `dotnet test tests/Leaderboard.E2ETests` **lanza la API compilada como un proceso aparte** (Kestrel y SQLite reales, en un puerto libre) y la detiene al final. Para ver la tabla de latencias en consola:

```bash
dotnet test tests/Leaderboard.E2ETests --logger "console;verbosity=detailed"
```

Para ejecutarlos **contra el contenedor Docker** (o cualquier despliegue), levanta el servicio con un rate limit alto, para no estrangular la simulación, y apunta los tests a él (PowerShell):

```bash
$env:LEADERBOARD_RATE_LIMIT="1000000"; $env:LEADERBOARD_RATE_BURST="1000000"; docker compose -f deploy/docker-compose.yml up --build -d
```

```bash
$env:E2E_BASE_URL="http://localhost:8080"; $env:E2E_WRITER_KEY="demo-writer-key"; $env:E2E_READER_KEY="demo-reader-key"; dotnet test tests/Leaderboard.E2ETests --logger "console;verbosity=detailed"
```

| Variable | Default | Uso |
|---|---|---|
| `E2E_BASE_URL` | _vacío: lanza proceso local_ | URL del servicio a probar |
| `E2E_WRITER_KEY` / `E2E_READER_KEY` | keys del proceso local | Keys del servicio externo |
| `E2E_WRITES` / `E2E_USERS` / `E2E_CONCURRENCY` | `5000` / `500` / `64` | Volumen de la simulación |
| `E2E_REPORT_PATH` | `bin/.../e2e-report.md` | Archivo donde se acumula la tabla de latencias |

Cada ejecución usa ids de usuario con un prefijo único, así que se puede repetir contra un despliegue compartido sin colisiones. Resultados medidos: [SOLUTION.md §9](SOLUTION.md#9-latencia-y-throughput).

---

## Estructura del repositorio

```
src/
  Leaderboard.Domain/          Reglas puras: eventos, buckets, ventana, ranking
  Leaderboard.Application/     Casos de uso y puertos (interfaces)
  Leaderboard.Infrastructure/  Adaptadores: SQLite, caché
  Leaderboard.Api/             Minimal API, auth, rate limit, observabilidad
tests/
  Leaderboard.UnitTests/
  Leaderboard.IntegrationTests/
  Leaderboard.E2ETests/
docs/
  ARCHITECTURE.md              Componentes y flujos (diagramas Mermaid)
  ADR/                         Registro de decisiones de arquitectura
  AI_LOG.md                    Bitácora de uso de IA (prompts y respuestas)
deploy/
  Dockerfile, docker-compose.yml
```

---

## Uso de IA

<!-- GUÍA (OBLIGATORIO): responder las 5 preguntas del enunciado. Detalle completo en docs/AI_LOG.md. -->

**¿Utilizaste IA?** Sí

**¿Cuáles modelos?**
- Claude Opus 5.5 (Anthropic)
- Claude Sonnet 5.5 (Anthropic)
- _TBD: agregar otros si se usan_

**¿Cuáles herramientas?**
- Claude Code (app de escritorio, pestaña Code)
- _TBD: IDE (Visual Studio / Rider / VS Code)_

**¿Para qué?**
- Análisis del enunciado y plan de trabajo
- Propuesta de organización de la solución .NET y plantillas de documentación
- Generación del esqueleto del repositorio
- _TBD: generación de código / tests / revisión / refactor_

**Prompts, respuestas y aclaraciones relevantes:** ver [docs/AI_LOG.md](docs/AI_LOG.md)
