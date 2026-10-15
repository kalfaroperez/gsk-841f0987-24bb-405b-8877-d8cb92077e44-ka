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
docker compose -f deploy/docker-compose.yml up --build
```

<!-- GUÍA: confirmar puerto real y URL de OpenAPI cuando estén implementados. -->
- API: `http://localhost:_TBD_`
- OpenAPI: `http://localhost:_TBD_/openapi/v1.json`
- Health: `http://localhost:_TBD_/health/ready`

---

## Configuración

<!-- GUÍA: una fila por opción de LeaderboardOptions / seguridad. Mantener sincronizado con appsettings.json. -->

| Variable | Descripción | Default |
|---|---|---|
| `Leaderboard__Window` | Ventana de agregación | `7.00:00:00` |
| `Leaderboard__BucketSize` | Tamaño del bucket temporal | `00:01:00` |
| `Leaderboard__MaxTop` | Máximo permitido para `top` | `_TBD_` |
| `Leaderboard__DefaultTop` | `top` por defecto | `_TBD_` |
| `Leaderboard__FutureSkewTolerance` | Tolerancia para timestamps futuros | `00:05:00` |
| `Leaderboard__CacheTtl` | TTL de caché del leaderboard | `00:00:01` |
| `Auth__ApiKeys__*` | API keys válidas | _ver SOLUTION.md_ |
| `RateLimit__PermitsPerSecond` | Límite por API key | `_TBD_` |

---

## Uso de la API

<!-- GUÍA: un curl por endpoint + respuesta de ejemplo REAL (copiada de una ejecución). -->

### Registrar puntuación

```bash
curl -X POST http://localhost:_TBD_/score -H "Content-Type: application/json" -H "X-Api-Key: _TBD_" -H "Idempotency-Key: 6f1c0e1a-0001" -d "{\"user_id\":\"alice\",\"score\":50,\"timestamp\":\"2026-10-15T12:00:00Z\"}"
```

```json
_TBD_
```

### Top N

```bash
curl "http://localhost:_TBD_/leaderboard?top=10" -H "X-Api-Key: _TBD_"
```

```json
_TBD_
```

### Score de un usuario

```bash
curl http://localhost:_TBD_/user/alice/score -H "X-Api-Key: _TBD_"
```

```json
_TBD_
```

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

<!-- GUÍA: describir qué cubre cada suite y qué salida esperar (n.º de tests, duración aproximada). -->

| Suite | Cubre |
|---|---|
| UnitTests | Lógica de agregación, ventana, buckets, desempates, idempotencia |
| IntegrationTests | Contratos HTTP, validación, auth, rate limit, ventana con reloj falso |
| E2ETests | Ingesta concurrente (sin pérdida de actualizaciones), idempotencia bajo carga, lecturas consistentes |

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
