# Arquitectura

<!-- GUÍA: los diagramas deben coincidir con el código final. Revisar al cerrar. -->

## 1. Componentes

```mermaid
flowchart TB
    subgraph Api[Leaderboard.Api]
        EP[Endpoints Minimal API]
        MW[Auth · Rate limit · Correlation Id · ProblemDetails]
        OBS[OpenTelemetry · Health checks]
    end
    subgraph App[Leaderboard.Application]
        UC1[SubmitScore]
        UC2[GetTopLeaderboard]
        UC3[GetUserScore]
        PORTS[[Puertos: IScoreWriteStore · ILeaderboardReadStore]]
    end
    subgraph Dom[Leaderboard.Domain]
        D1[ScoreEvent · UserId]
        D2[TimeBucket · AggregationWindow]
        D3[Ranking determinista · EventIdFactory]
    end
    subgraph Infra[Leaderboard.Infrastructure]
        SQL[SqliteScoreStore]
        CACHE[HybridCache top-N]
    end
    EP --> MW --> UC1 & UC2 & UC3
    UC1 & UC2 & UC3 --> Dom
    UC1 & UC2 & UC3 --> PORTS
    SQL -.implementa.-> PORTS
    CACHE -.decora.-> PORTS
    SQL --> DB[(SQLite WAL)]
```

| Componente | Responsabilidad |
|---|---|
| _TBD_ | _TBD_ |

## 2. Flujo de escritura: `POST /score`

```mermaid
sequenceDiagram
    participant C as Cliente
    participant A as Api
    participant U as SubmitScore
    participant S as SqliteScoreStore
    C->>A: POST /score (+ Idempotency-Key)
    A->>A: Auth · rate limit · validación
    A->>U: comando
    U->>U: EventId · bucket · reglas de tiempo
    U->>S: Append(evento)
    S->>S: BEGIN
    S->>S: INSERT OR IGNORE score_events
    alt evento nuevo
        S->>S: UPSERT score_buckets total = total + n
    end
    S->>S: COMMIT
    S-->>U: Created | Duplicate
    U-->>A: resultado
    A-->>C: 201 | 200
```

## 3. Flujo de lectura: `GET /leaderboard?top=N`

```mermaid
sequenceDiagram
    participant C as Cliente
    participant A as Api
    participant H as HybridCache
    participant S as SqliteScoreStore
    C->>A: GET /leaderboard?top=N
    A->>H: GetOrCreate(key)
    alt cache miss (una sola ejecución por key)
        H->>S: SUM(buckets en ventana) ORDER BY LIMIT
        S-->>H: top-N
    end
    H-->>A: top-N
    A-->>C: 200 (+ windowStart, windowEnd, generatedAt)
```

## 4. Flujo de lectura: `GET /user/{id}/score`

_TBD_

## 5. Decisiones

Ver [ADR/](ADR/).
