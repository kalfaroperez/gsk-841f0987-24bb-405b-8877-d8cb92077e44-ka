# ADR-003: Agregación por ventana mediante buckets temporales

- **Estado:** Aceptado
- **Fecha:** 2026-10-15

## Contexto

El leaderboard suma los puntos por usuario en una ventana configurable (p. ej. 7 días) y debe actualizarse en near-real-time. Sumar eventos crudos en cada lectura tiene un costo proporcional al volumen de eventos, que no está acotado.

## Decisión

- Los puntos se pre-agregan por `(user_id, bucket_start)`, donde `bucket_start` es el instante del evento (no el de ingesta) truncado a `BucketSize` en UTC.
- La ventana es el intervalo semiabierto `[Start, End)` alineado a buckets: `End` = fin del bucket actual y `Start = End − Window`. Siempre contiene el instante actual y cubre exactamente `Window / BucketSize` buckets.
- Restricciones validadas al arrancar: `BucketSize` divide un día (los límites quedan alineados a medianoche UTC y son idénticos en todos los nodos) y `Window` es múltiplo de `BucketSize`.
- `WindowAggregation.Totals` es la especificación ejecutable. Los adaptadores de almacenamiento implementan la misma regla en su lenguaje (SQL) y se prueban contra ella.

## Alternativas consideradas

| Alternativa | Pros | Contras |
|---|---|---|
| Sumar eventos crudos en cada lectura | Precisión exacta | Costo O(eventos); inviable a escala |
| Contador acumulado sin ventana | O(1) | No soporta "últimos N días" |
| Ventana deslizante exacta + resta de expirados | Precisa | Requiere un job de expiración y es propensa a inconsistencias |
| **Buckets alineados** | Costo acotado por `Window/BucketSize`; agregación incremental atómica | El borde antiguo avanza en saltos de `BucketSize` |

## Consecuencias

- Costo de lectura acotado: con 7 días y buckets de 1 minuto son 10.080 buckets por usuario como máximo, independiente del número de eventos.
- Precisión: el borde antiguo de la ventana tiene una granularidad de `BucketSize` (1 minuto por defecto), un error aceptable para un ranking semanal.
- Los eventos tardíos se aceptan y van a su bucket histórico. Si quedan fuera de la ventana, no suman en ella pero sí en el total histórico.
- Implementación: `TimeBucket.cs`, `AggregationWindow.cs`, `WindowAggregation.cs`. Evidencia: `TimeBucketTests`, `AggregationWindowTests`, `WindowAggregationTests`.
