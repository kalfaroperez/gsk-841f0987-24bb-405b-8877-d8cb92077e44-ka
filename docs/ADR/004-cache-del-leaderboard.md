# ADR-004: Caché del leaderboard como mitigación de hot key de lectura

- **Estado:** Aceptado
- **Fecha:** 2026-10-15

## Contexto

`GET /leaderboard` es el mismo para todos los clientes: unas pocas claves (`top=10`, `top=100`) concentran casi todas las lecturas. Sin caché, cada petición repite la misma agregación sobre la ventana. Peor aún, cuando una entrada expira, muchas peticiones simultáneas la recalcularían a la vez (estampida, o *cache stampede*).

## Decisión

- Un decorador `CachedLeaderboardReadStore` sobre el puerto `ILeaderboardReadStore` cachea el top-N con `HybridCache` y un TTL corto (`Leaderboard:CacheTtl`, 1 s por defecto; `0` lo desactiva).
- **Clave:** `leaderboard:top:{window.Start}:{window.End}:{top}`. Cuando la ventana avanza al siguiente bucket, la clave cambia, así que nunca se sirve una ventana vieja.
- `HybridCache` **coalesce** los misses concurrentes de la misma clave: una sola consulta al store, aunque lleguen 50 peticiones a la vez.
- El snapshot conserva su `AsOf` (el momento de lectura) y se expone como `generated_at`, así que la obsolescencia es visible para el cliente.
- `LeaderboardSnapshot` es `sealed` y `[ImmutableObject(true)]`, de modo que la caché en proceso reutiliza la instancia sin serializarla.
- `GET /user/{id}/score` **no** se cachea: es por clave, barato, y el usuario espera ver sus propias escrituras.

## Alternativas consideradas

| Alternativa | Pros | Contras |
|---|---|---|
| Sin caché | Frescura total | Trabajo repetido; estampida al escalar |
| `IMemoryCache` simple | Conocido | Sin protección contra estampida: N misses simultáneos = N consultas |
| Invalidar en cada escritura | Siempre fresco | Con escrituras constantes, la caché nunca acierta |
| Snapshot materializado por un job (cada ~1 s) | Lecturas O(1), carga constante | Proceso extra; innecesario a la escala del prototipo |
| **`HybridCache` + TTL corto** | Coalescencia incluida; L2 distribuido (Redis) se agrega sin tocar el código | Obsolescencia de hasta `CacheTtl` |

## Consecuencias

- **Garantía explícita:** el leaderboard puede estar desfasado hasta `CacheTtl` (1 s). Es compatible con "near-real-time" y queda documentado en SOLUTION.md §5.
- La carga de lectura sobre el store queda acotada a ~1 consulta por segundo por cada `top` distinto, sin importar el tráfico.
- **En producción:** el mismo decorador, con L2 en Redis, comparte el resultado entre réplicas. Opcionalmente, un snapshot materializado.
- **Evidencia:** `CachedLeaderboardReadStoreTests` (50 peticiones concurrentes → 1 consulta; claves separadas por `top` y por ventana; las puntuaciones de usuario no se cachean) y `Leaderboard_reflects_new_scores_once_the_cached_window_moves`.
