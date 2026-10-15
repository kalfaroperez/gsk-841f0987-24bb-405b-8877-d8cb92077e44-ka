# ADR-005: Escritura síncrona vs micro-batching

- **Estado:** Aceptado para el prototipo (síncrona). Group commit propuesto como siguiente paso.
- **Fecha:** 2026-10-15

## Contexto

Cada `POST /score` debe ser durable cuando se responde `2xx` y contarse exactamente una vez. Con SQLite hay **un solo escritor** y, con `synchronous=FULL`, un `fsync` por commit. Las mediciones E2E (SOLUTION.md §9) muestran que ese `fsync` es el cuello de botella: ~390 escrituras/s en local y ~120 en Docker Desktop, con colas largas cuando hay mucha concurrencia.

## Decisión

**Prototipo: escritura síncrona, una transacción por evento.** El `2xx` se responde después del `COMMIT`. Un semáforo en proceso serializa a los escritores para evitar contención de locks; la exactitud la da la transacción `IMMEDIATE` (ver ADR-001).

**Evolución propuesta: group commit.** Un `Channel<T>` acotado recibe los eventos, y un único consumidor toma los que llegan en una ventana de 1–5 ms (o hasta N eventos). Los confirma en **una** transacción con **un** `fsync`, y después completa el `TaskCompletionSource` de cada petición con su resultado (`Created` o `Duplicate`).

## Alternativas consideradas

| Alternativa | Pros | Contras |
|---|---|---|
| **Síncrona, una transacción por evento** (elegida) | Simple, fácil de razonar y probar; durabilidad por evento | Throughput limitado a ~1 / latencia del `fsync` |
| `synchronous=NORMAL` | Mucho más throughput en WAL | Una caída de energía puede perder transacciones ya confirmadas con `2xx`: rompe la garantía |
| Responder `202` y escribir en segundo plano (fire-and-forget) | Latencia mínima | El `2xx` ya no implica durabilidad; perder el proceso pierde eventos aceptados |
| **Group commit** (propuesta) | Throughput × tamaño del lote, misma garantía de durabilidad, mismo contrato HTTP | Más complejidad: backpressure (canal acotado → `503`/`429` si se llena), cancelación, ventana de espera que añade 1–5 ms de latencia |

## Consecuencias

- Se mantiene una garantía honesta y simple: lo confirmado está en disco. El costo es throughput, medido y documentado.
- El group commit no requiere cambios en el dominio, los casos de uso ni la API: es interno a `SqliteScoreStore` (o un decorador de `IScoreWriteStore`), gracias a los puertos.
- En producción (SOLUTION.md §8), la misma idea se aplica a escala: el log replicado es el "commit" durable y los agregadores agrupan escrituras en micro-batches de 200 ms.
