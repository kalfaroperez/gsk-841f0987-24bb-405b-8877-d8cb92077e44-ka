# Bitácora de uso de IA

<!--
GUÍA
- Registrar cada interacción relevante MIENTRAS se trabaja, no al final.
- La columna "Qué acepté / modifiqué / rechacé" es la más importante: demuestra criterio propio.
- Los prompts pueden ser literales o un resumen fiel; las respuestas, resumidas.
-->

## Resumen

| Modelo | Herramienta | Uso principal |
|---|---|---|
| Claude Sonnet 5.5 | Claude Code (desktop) | Análisis del enunciado, plan de trabajo |
| Claude Opus 5.5 | Claude Code (desktop) | Organización de la solución .NET, plantillas de documentación, esqueleto del repo |

---

## Entrada 1: Análisis del enunciado y plan de trabajo

- **Fecha / fase:** 2026-10-15 · Planificación
- **Modelo / herramienta:** Claude Sonnet 5.5 · Claude Code
- **Prompt (literal):**
  > Basado en este test para GoSkale evaluar de que manera puedo hacer un plan de trabajo para iniciar desde 0 y completar cumpliendo los requerimientos descritos en ambos archivos word. Se critico, teniendo en cuenta que la evaluación sera para un cargo de desarrollador Senior, que sea capaz de aportar valor a nuevos retos de integracion con diferentes compañias. NO hagas codigo aun, solo elabora un plan de trabajo de inicio a fin
- **Respuesta (resumen):** Identificó lo que realmente se evalúa (correctitud concurrente, idempotencia, ventana eficiente, hot keys, cálculos de escala, trazabilidad de IA). Listó ambigüedades del enunciado (falta de id de evento, semántica de timestamp, 404 vs 0, tipo de score, desempates, límite de top). Propuso SQLite WAL + buckets temporales, plan por fases de ~6 h, cuatro puntos de hot key, cálculos para 100k req/s y una checklist de entrega.
- **Qué acepté / modifiqué / rechacé:** _TBD_

## Entrada 2: Organización de la solución en .NET y guía de documentación

- **Fecha / fase:** 2026-10-15 · Diseño
- **Modelo / herramienta:** Claude Opus 5.5 · Claude Code
- **Prompt (literal):**
  > Como un desarrollador .net Senior con experiencia en C# organiza las ideas punto a punto dentro de una solucion acorde al requerimiento. Guia y orienta de que manera puedo ir documentando el archivo Solution.md y Readme.md basado en las indicaciones dadas.
- **Respuesta (resumen):** Propuso .NET 10 LTS con Minimal APIs, arquitectura hexagonal ligera (Domain / Application / Infrastructure / Api), SQLite + Dapper (SQL explícito para el incremento atómico), `TimeProvider` para testear ventanas, `HybridCache` contra estampida, rate limiting y OpenTelemetry nativos, y xUnit + `WebApplicationFactory`. Incluyó estrategia de tests mapeada al requerimiento y esqueletos de README y SOLUTION con preguntas guía.
- **Qué acepté / modifiqué / rechacé:** _TBD_

## Entrada 3: Esqueleto del repositorio

- **Fecha / fase:** 2026-10-15 · Setup
- **Modelo / herramienta:** Claude Opus 5.5 · Claude Code
- **Prompt (literal):**
  > armalo
- **Respuesta (resumen):** Generó la solución `Leaderboard.slnx` con 4 proyectos `src/` y 3 de `tests/`, referencias según la regla de dependencias, `global.json`, `Directory.Build.props` (nullable, warnings as errors, analizadores), plantillas de README, SOLUTION, ARCHITECTURE (diagramas Mermaid), ADRs y esta bitácora, y placeholders de `deploy/`.
- **Qué acepté / modifiqué / rechacé:** _TBD_

## Entrada 4: Creación del repositorio público

- **Fecha / fase:** 2026-10-15 · Setup
- **Modelo / herramienta:** Claude Opus 5.5 · Claude Code
- **Prompt (literal):**
  > Primero crea el repo público en GitHub y sube el commit
- **Respuesta (resumen):** Instaló GitHub CLI con winget. La autenticación (`gh auth login`) la hice yo de forma manual; la IA no manejó credenciales. Creó el repo público, subió `main` y verificó que se clona sin credenciales.
- **Qué acepté / modifiqué / rechacé:** _TBD_

## Entrada 5: Fase de Domain con TDD

- **Fecha / fase:** 2026-10-15 · Implementación (Domain)
- **Modelo / herramienta:** Claude Opus 5.5 · Claude Code
- **Prompt (literal):**
  > Empieza la fase de Domain con sus tests unitarios (TDD)
- **Respuesta (resumen):** Fijó las reglas de dominio (ventana alineada a buckets `[Start, End)`, puntos `long` no negativos con tope, `EventId` con prefijos `k:`/`h:`, ranking con orden total). Escribió primero 69 tests unitarios y confirmó la fase roja (36 errores de compilación). Después implementó `UserId`, `EventId`, `LeaderboardRules`, `TimeBucket`, `AggregationWindow`, `ScoreEvent`, `WindowAggregation` y `Ranking` (top-N con heap acotado) hasta la fase verde: 69/69 tests y compilación sin advertencias con analizadores `latest-recommended`.
- **Aclaraciones relevantes:**
  - El analizador CA1036 exigió operadores de comparación en `UserId`, y se agregaron.
  - CA1707 (guiones bajos en nombres) se desactivó solo para `tests/`, por la convención de nombres descriptivos en xUnit.
  - Se detectó que los ADRs del commit inicial tenían la codificación corrupta (PowerShell 5.1 leyó UTF-8 como ANSI), y se regeneraron.
- **Qué acepté / modifiqué / rechacé:** _TBD_

## Entrada 6: Application y persistencia SQLite

- **Fecha / fase:** 2026-10-15 · Implementación (Application + Infrastructure)
- **Modelo / herramienta:** Claude Opus 5.5 · Claude Code
- **Prompt (literal):**
  > Empieza la fase de Application y la persistencia SQLite (puertos, casos de uso, adapter con incremento atómico) con sus tests
- **Respuesta (resumen):** Definió los puertos `IScoreWriteStore` e `ILeaderboardReadStore`, los casos de uso `SubmitScore`, `GetLeaderboard` y `GetUserScore` (con `TimeProvider` inyectado) y `LeaderboardQueryLimits`. Implementó el adaptador SQLite: WAL, `BEGIN IMMEDIATE`, `ON CONFLICT DO NOTHING` para deduplicar, upsert con incremento en SQL, `CHECK` contra el overflow silencioso, ticks UTC como `INTEGER` e índice cubriente. Escribió 15 tests de casos de uso con un store en memoria y 10 de integración: 3 de concurrencia real y uno que compara el SQL contra la implementación de referencia del dominio.
- **Aclaraciones relevantes:**
  - Los tests se escribieron con `TestContext.Current` (xUnit v3), pero el proyecto usa xUnit 2.9; se reemplazó por `CancellationToken.None`.
  - Se hizo un experimento para no sobre-afirmar: al quitar el semáforo de escritura, los tests de concurrencia siguen pasando. La exactitud viene de la transacción `IMMEDIATE`, no del semáforo, y así quedó documentado.
  - Decisión `404` vs `0`: `404` solo si el usuario nunca envió puntuaciones.
- **Qué acepté / modifiqué / rechacé:** _TBD_

## Entrada 7: Capa Api

- **Fecha / fase:** 2026-10-15 · Implementación (Api)
- **Modelo / herramienta:** Claude Opus 5.5 · Claude Code
- **Prompt (literal):**
  > Empieza la fase de Api: endpoints, ProblemDetails, API key, rate limit, caché del top-N, health y OpenTelemetry, con tests de integración HTTP
- **Respuesta (resumen):** Primero escribió 29 tests HTTP con `WebApplicationFactory` (SQLite temporal, `FakeTimeProvider`, keys de prueba). Después implementó los 3 endpoints con contratos en snake_case, errores RFC 9457 con `error_codes` estables, autenticación por API key con comparación en tiempo constante y roles, rate limit con token bucket por cliente, caché del top-N con `HybridCache` (coalescencia de misses), health live/ready, OpenTelemetry (Prometheus y OTLP opcional), correlation id y validación de configuración al arrancar. Hizo una prueba de humo con `dotnet run` y `curl`.
- **Aclaraciones relevantes:**
  - La configuración se resuelve perezosamente desde DI (Options + `ValidateOnStart`) en lugar de leerse antes de `Build()`, para que los tests puedan sobrescribirla.
  - Se cambió el puerto de lectura para devolver `LeaderboardSnapshot` con `AsOf`. Así `generated_at` refleja el momento real de lectura aunque la respuesta venga de caché, en lugar de mentir sobre la frescura.
  - El test de rate limit se rediseñó para no depender del tiempo (se valida sobre la propia ráfaga).
  - La primera versión de los tests usaba inicializadores de campo que referenciaban otros campos (CS0236); se movieron a los constructores.
- **Qué acepté / modifiqué / rechacé:** _TBD_

## Entrada 8: Tests E2E, medición de latencias y verificación de Docker

- **Fecha / fase:** 2026-10-15 · Tests E2E y despliegue
- **Modelo / herramienta:** Claude Opus 5.5 · Claude Code
- **Prompt (literal):**
  > Sigue con los tests E2E de carga contra el servicio en ejecución, mide latencias reales y verifica el despliegue con Docker
- **Respuesta (resumen):** Creó el proyecto E2E con un fixture que lanza la API compilada como proceso aparte (o apunta a `E2E_BASE_URL`), un medidor de latencias (p50/p95/p99, throughput, 5xx) y 5 escenarios: recorrido completo, límites de seguridad, carga con hot user y reintentos (verificando exactamente todos los totales), lecturas durante escrituras y tormenta de reintentos. Corrigió el Dockerfile (volumen `/data` con dueño no-root 1654, `.dockerignore`) y el compose (keys de demo configurables). Ejecutó la suite contra el proceso local y contra el contenedor, y documentó los números.
- **Aclaraciones relevantes:**
  - Hubo que iniciar Docker Desktop, que no estaba corriendo.
  - Se midió también con concurrencia 1 para separar el costo del servicio (p50 de 5.6 ms) del costo de la cola. La latencia alta con 64 clientes es cola frente al único escritor (ley de Little), no lentitud del código.
  - En Docker Desktop el throughput de escritura cae a ~120 req/s y el p99 llega a ~10 s. Se documentó tal cual, junto con la mejora concreta (group commit), en lugar de ocultarlo o ajustar el test para que se viera mejor.
- **Qué acepté / modifiqué / rechacé:** _TBD_

## Entrada 9: Escalado, despliegue, recuperación y resumen ejecutivo

- **Fecha / fase:** 2026-10-15 · Documentación
- **Modelo / herramienta:** Claude Opus 5.5 · Claude Code
- **Prompt (literal):**
  > Completa SOLUTION §8 (escala a 100k req/s con cálculos), §12, §13, §14 y el resumen ejecutivo
- **Respuesta (resumen):**
  - §8: supuestos explícitos y cálculos de ancho de banda, retención del log, pods API (con tolerancia a la caída de una zona), particiones, agregadores, ops y memoria de Redis, estado de deduplicación y almacenamiento; arquitectura objetivo y cuellos de botella con mitigaciones.
  - §12: pipeline CI/CD con E2E contra el contenedor, canary con criterios automáticos, migraciones expand/contract y las limitaciones de desplegar el prototipo con SQLite.
  - §13: rollback de código y recuperación de datos con RPO/RTO por escenario.
  - §14: limitaciones y trabajo futuro orientado a integraciones con varias compañías (multi-tenancy, contrato versionado, ingesta por lotes).
- **Aclaraciones relevantes:**
  - Se decidió y justificó un cambio de contrato para producción (`202 Accepted` con deduplicación asíncrona), porque deduplicar de forma síncrona antes del log tiene un modo de falla que pierde eventos.
  - Los números de capacidad son supuestos conservadores declarados, no mediciones. Las mediciones reales del prototipo están en §9.
- **Qué acepté / modifiqué / rechacé:** _TBD_

---

## Plantilla de entrada

```
## Entrada N: <tema>

- **Fecha / fase:**
- **Modelo / herramienta:**
- **Prompt:**
- **Respuesta (resumen):**
- **Qué acepté / modifiqué / rechacé:**
```
