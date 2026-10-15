# ADR-002: Idempotencia de escritura (Idempotency-Key y hash determinista)

- **Estado:** Aceptado
- **Fecha:** 2026-10-15

## Contexto

El payload de `POST /score` (`user_id`, `score`, `timestamp`) no incluye un identificador de evento. Ante un timeout, el cliente reintenta y, sin una identidad del evento, el servicio sumaría la puntuación dos veces. El requisito "no perder actualizaciones ni producir estados inconsistentes" incluye no duplicarlas.

## Decisión

Cada evento tiene un `EventId` único, persistido con restricción de unicidad:

1. Si el cliente envía el header `Idempotency-Key`, el id es `k:<key>` (1–128 caracteres ASCII imprimibles, sin espacios).
2. Si no, el id es `h:<sha256>` de la forma canónica `v1|user_id|score|utcTicks`. El instante se normaliza a UTC, así que el mismo momento con distinto offset produce el mismo id.

Los prefijos `k:` y `h:` separan los dos espacios de nombres para que nunca colisionen. El prefijo `v1` permite cambiar la forma canónica sin invalidar ids antiguos.

## Alternativas consideradas

| Alternativa | Pros | Contras |
|---|---|---|
| Sin idempotencia | Simple | Reintentos duplican puntuación: viola la correctitud |
| Solo `Idempotency-Key` obligatorio | Semántica exacta | Cambia el contrato del enunciado; clientes existentes fallarían |
| Solo hash del payload | Transparente para el cliente | Dos eventos legítimos idénticos colisionan |
| **Key opcional + hash de respaldo** | Compatible con el contrato y exacto cuando el cliente coopera | El respaldo mantiene la colisión de eventos idénticos (documentada) |

## Consecuencias

- Exactamente-una-vez por `EventId`. La garantía final depende de que la persistencia inserte evento y agregado en la misma transacción (ver ADR-001).
- Eventos legítimamente idénticos sin `Idempotency-Key` (mismo usuario, puntos e instante con resolución de tick) se cuentan una vez. Se considera aceptable y se recomienda a los integradores enviar la key.
- La tabla de ids crece con el volumen. En producción se acota con un TTL de deduplicación (p. ej. 24–72 h) en un almacén rápido.
- Implementación: `src/Leaderboard.Domain/EventId.cs`. Evidencia: `tests/Leaderboard.UnitTests/Domain/EventIdTests.cs`.
