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
