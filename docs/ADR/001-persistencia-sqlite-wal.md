# ADR-001: Persistencia del prototipo con SQLite en modo WAL

- **Estado:** Aceptado
- **Fecha:** 2026-10-15

## Contexto

El prototipo lo clona y ejecuta un evaluador. Cada dependencia externa (servidor de base de datos, Redis) es un punto de fallo en esa primera ejecución. A la vez, la solución debe demostrar ingesta concurrente sin pérdidas ni estados inconsistentes, y el enunciado permite SQLite, PostgreSQL, Redis o archivos.

## Decisión

- SQLite embebido (`Microsoft.Data.Sqlite`) en modo **WAL**, con acceso mediante **Dapper** y SQL explícito.
- **Escritura:** una transacción `BEGIN IMMEDIATE` que inserta el evento (`ON CONFLICT DO NOTHING`) y, solo si se insertó, incrementa `score_buckets` y `user_totals` en SQL (`total = total + excluded.total`).
- **Lectura:** una sola sentencia por consulta, es decir, un único snapshot WAL.
- `synchronous = FULL`, `busy_timeout = 5000` ms y un semáforo en proceso que serializa a los escritores.
- El adaptador implementa los puertos `IScoreWriteStore` e `ILeaderboardReadStore`; cambiarlo no toca dominio ni casos de uso.

## Alternativas consideradas

| Alternativa | Pros | Contras |
|---|---|---|
| PostgreSQL en Docker | Más cercano a producción; múltiples escritores | Obliga al evaluador a tener Docker; más piezas que pueden fallar |
| Redis (sorted sets) | Rapidísimo; `ZINCRBY` atómico | La ventana por tiempo requiere sets por bucket y `ZUNIONSTORE`; durabilidad configurable; dependencia externa |
| Archivos / memoria | Trivial | Sin transacciones ni durabilidad; habría que reimplementar la atomicidad |
| EF Core | Productividad, migraciones | Oculta el SQL del incremento atómico, que es justo lo que se quiere demostrar |
| **SQLite WAL + Dapper** | Cero dependencias, ACID real, SQL visible | Un solo escritor; no escala horizontalmente |

## Consecuencias

- Se ejecuta con `dotnet run` sin servicios externos.
- La exactitud se verifica con tests de concurrencia reales sobre un archivo (no `:memory:`), así que WAL, bloqueos y pooling se comportan como en producción.
- Verificado experimentalmente: sin el semáforo, los tests de concurrencia siguen pasando porque `BEGIN IMMEDIATE` serializa a los escritores. El semáforo es una optimización y no la fuente de la exactitud.
- **Límite:** un único escritor por archivo. El throughput de escritura queda acotado por el `fsync` por transacción. La ruta de escalado (log particionado, agregadores, Redis Cluster, store durable) está en SOLUTION.md §8.
- El incremento en SQL hace que el patrón sea portable a Postgres con READ COMMITTED sin bloqueos explícitos de fila.
