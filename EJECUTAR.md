# Cómo ejecutar la prueba en tu máquina

Guía rápida para clonar, levantar y probar el servicio en **10 minutos**. Funciona en Windows, macOS y Linux.

Hay dos caminos. Elige **uno**:

| Camino | Necesitas | Ideal si… |
|---|---|---|
| **A. .NET** (recomendado) | .NET SDK 9 o 10 | Quieres correr también los tests |
| **B. Docker** | Docker Desktop | No quieres instalar .NET |

---

## Paso 0: Clonar el repositorio

```bash
git clone https://github.com/kalfaroperez/gsk-841f0987-24bb-405b-8877-d8cb92077e44-ka.git
```

```bash
cd gsk-841f0987-24bb-405b-8877-d8cb92077e44-ka
```

> Todos los comandos siguientes se ejecutan **desde esta carpeta** (la raíz del repo).

---

## Camino A: con .NET

### A1. Verificar el SDK

```bash
dotnet --version
```

Debe mostrar **9.0.algo o superior** (por ejemplo `9.0.100`, `9.0.300` o `10.0.100`). El proyecto apunta a `net9.0` y también funciona en máquinas que solo tienen .NET 10. Si no tienes ninguno, descarga el **SDK** (no solo el runtime) de .NET 9 o 10 desde https://dotnet.microsoft.com/download.

### A2. Ejecutar todos los tests

```bash
dotnet test
```

La primera vez descarga los paquetes NuGet (~1 min). Después verás **tres líneas** como estas:

```
Correctas! - Con error: 0, Superado: 84 ...  Leaderboard.UnitTests.dll
Correctas! - Con error: 0, Superado: 39 ...  Leaderboard.IntegrationTests.dll
Correctas! - Con error: 0, Superado:  5 ...  Leaderboard.E2ETests.dll
```

(En Windows en inglés dice `Passed!` en vez de `Correctas!`.)

| Suite | Qué prueba | Tiempo |
|---|---|---|
| Unit (84) | Reglas de negocio: ventana de 7 días, buckets, idempotencia, ranking | < 1 s |
| Integration (39) | SQLite real con **1.000 escrituras concurrentes**, y la API HTTP completa | ~10 s |
| E2E (5) | Levanta la API **como proceso real** y le lanza **5.000 peticiones**; mide latencias | 20–60 s |

**Ver la tabla de latencias de la carga E2E** (opcional):

```bash
dotnet test tests/Leaderboard.E2ETests --logger "console;verbosity=detailed"
```

### A3. Levantar la API

```bash
dotnet run --project src/Leaderboard.Api
```

Cuando veas `Now listening on: http://localhost:8080`, la API está lista. **Deja esta terminal abierta** y sigue con la sección [Probar la API](#probar-la-api).

> **¿El puerto 8080 está ocupado?** Usa otro:
> `dotnet run --project src/Leaderboard.Api --urls http://localhost:5080`
> (y cambia `8080` por `5080` en los ejemplos).

Para detenerla: `Ctrl + C`.

---

## Camino B: con Docker

### B1. Verificar Docker

```bash
docker --version
```

Docker Desktop debe estar **abierto y corriendo** (en Windows usa WSL 2).

### B2. Construir y levantar

```bash
docker compose -f deploy/docker-compose.yml up --build -d
```

La primera vez tarda 2–3 minutos (descarga las imágenes de .NET). Comprueba que está arriba:

```bash
docker compose -f deploy/docker-compose.yml ps
```

Debe decir `Up`. La API queda en `http://localhost:8080`.

> ⚠️ **Con Docker las API keys son otras:** `demo-writer-key` y `demo-reader-key` (en lugar de `dev-…`). Ver la tabla de abajo.

Para detenerla:

```bash
docker compose -f deploy/docker-compose.yml down
```

---

## Probar la API

### API keys

| Key | Permisos | Válida en |
|---|---|---|
| `dev-ingestor-key` | escribir + leer | Camino A (.NET) |
| `dev-reader-key` | solo leer | Camino A (.NET) |
| `demo-writer-key` | escribir + leer | Camino B (Docker) |
| `demo-reader-key` | solo leer | Camino B (Docker) |

Se envían en el header `X-Api-Key`.

### Opción 1: archivo `.http` (lo más fácil)

Abre **`src/Leaderboard.Api/Leaderboard.Api.http`**:

- **Visual Studio 2022**: abre el archivo y haz clic en **"Send request"** sobre cada petición.
- **VS Code**: instala la extensión **REST Client** (`humao.rest-client`) y haz clic en **"Send Request"**.

Tiene 12 peticiones listas, en orden: registrar puntuaciones, reintento idempotente, leaderboard, score de usuario, errores (400, 401, 403, 404), métricas y OpenAPI.

> Si usas Docker, cambia arriba del archivo `@writerKey = demo-writer-key` y `@readerKey = demo-reader-key`.

### Opción 2: desde la terminal

<details open>
<summary><b>Windows (PowerShell)</b></summary>

```powershell
$base = "http://localhost:8080"; $w = @{ "X-Api-Key" = "dev-ingestor-key" }; $r = @{ "X-Api-Key" = "dev-reader-key" }
```

Registrar dos puntuaciones:

```powershell
$now = (Get-Date).ToUniversalTime().ToString("o")
Invoke-RestMethod -Method Post "$base/score" -Headers $w -ContentType "application/json" -Body "{`"user_id`":`"alice`",`"score`":50,`"timestamp`":`"$now`"}"
Invoke-RestMethod -Method Post "$base/score" -Headers $w -ContentType "application/json" -Body "{`"user_id`":`"bob`",`"score`":80,`"timestamp`":`"$now`"}"
```

Ver el top y el score de un usuario:

```powershell
Invoke-RestMethod "$base/leaderboard?top=10" -Headers $r | ConvertTo-Json -Depth 5
Invoke-RestMethod "$base/user/alice/score" -Headers $r
```

</details>

<details>
<summary><b>macOS / Linux / Git Bash (curl)</b></summary>

```bash
NOW=$(date -u +%Y-%m-%dT%H:%M:%SZ)
curl -s -X POST http://localhost:8080/score -H "Content-Type: application/json" -H "X-Api-Key: dev-ingestor-key" -d "{\"user_id\":\"alice\",\"score\":50,\"timestamp\":\"$NOW\"}"
curl -s -X POST http://localhost:8080/score -H "Content-Type: application/json" -H "X-Api-Key: dev-ingestor-key" -d "{\"user_id\":\"bob\",\"score\":80,\"timestamp\":\"$NOW\"}"
```

```bash
curl -s "http://localhost:8080/leaderboard?top=10" -H "X-Api-Key: dev-reader-key"
curl -s http://localhost:8080/user/alice/score -H "X-Api-Key: dev-reader-key"
```

</details>

**Resultado esperado del leaderboard:**

```json
{
  "window_start": "…", "window_end": "…", "generated_at": "…",
  "entries": [
    { "rank": 1, "user_id": "bob",   "score": 80 },
    { "rank": 2, "user_id": "alice", "score": 50 }
  ]
}
```

> El leaderboard se cachea **1 segundo**. Si consultas justo después de escribir y no ves el cambio, repite la consulta un segundo después. Es intencional y está documentado (`generated_at` indica cuándo se leyeron los datos).

### Endpoints públicos útiles

| URL | Qué es |
|---|---|
| http://localhost:8080/health/ready | Estado del servicio y la base de datos |
| http://localhost:8080/metrics | Métricas Prometheus (busca `leaderboard_scores_ingested_total`) |
| http://localhost:8080/openapi/v1.json | Contrato OpenAPI de la API |

---

## Opcional: E2E contra el contenedor Docker

Comprueba que la imagen Docker se comporta igual que en local. Sube el rate limit para que no estrangule la simulación de carga.

**Windows (PowerShell):**

```powershell
$env:LEADERBOARD_RATE_LIMIT="1000000"; $env:LEADERBOARD_RATE_BURST="1000000"; docker compose -f deploy/docker-compose.yml up --build -d
$env:E2E_BASE_URL="http://localhost:8080"; $env:E2E_WRITER_KEY="demo-writer-key"; $env:E2E_READER_KEY="demo-reader-key"
dotnet test tests/Leaderboard.E2ETests --logger "console;verbosity=detailed"
```

**macOS / Linux:**

```bash
LEADERBOARD_RATE_LIMIT=1000000 LEADERBOARD_RATE_BURST=1000000 docker compose -f deploy/docker-compose.yml up --build -d
E2E_BASE_URL=http://localhost:8080 E2E_WRITER_KEY=demo-writer-key E2E_READER_KEY=demo-reader-key dotnet test tests/Leaderboard.E2ETests --logger "console;verbosity=detailed"
```

---

## Problemas frecuentes

| Síntoma | Causa | Solución |
|---|---|---|
| `A compatible .NET SDK was not found` | No tienes el SDK de .NET 9 ni de .NET 10 | Instala el **SDK 9 o 10** (enlace en A1) |
| `address already in use` / la API no arranca | Puerto 8080 ocupado | Usa `--urls http://localhost:5080` (A3) o detén el contenedor Docker si quedó corriendo |
| `401 Unauthorized` | Falta la key o es de otro camino | Usa `dev-…` con .NET y `demo-…` con Docker |
| `403 Forbidden` al hacer POST | Estás usando la key de solo lectura | Usa `dev-ingestor-key` / `demo-writer-key` |
| Docker: `failed to connect to the docker API` | Docker Desktop no está abierto | Ábrelo y espera a que diga *Engine running* |
| En PowerShell, `curl` da un error raro | En Windows PowerShell `curl` es un alias de `Invoke-WebRequest` | Usa los ejemplos de PowerShell, o `curl.exe` |
| Quiero empezar con datos limpios | La base de datos persiste entre ejecuciones | .NET: borra `src/Leaderboard.Api/data/`. Docker: `docker compose -f deploy/docker-compose.yml down -v` |

---

## ¿Y después?

- **[README.md](README.md)**: referencia completa (configuración, contratos, códigos de estado).
- **[SOLUTION.md](SOLUTION.md)**: decisiones, garantías de consistencia, hot keys, escalado a 100k req/s, latencias medidas, seguridad y despliegue.
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**: diagramas de componentes y flujos.
