using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Leaderboard.E2ETests;

/// <summary>
/// The real service over real HTTP. Two modes:
/// <list type="bullet">
/// <item>E2E_BASE_URL set: target an already running deployment (e.g. the Docker container).
/// E2E_WRITER_KEY / E2E_READER_KEY provide its API keys.</item>
/// <item>Otherwise: launch the built Leaderboard.Api as a separate OS process (Kestrel, real SQLite file)
/// on a free port, and tear it down afterwards.</item>
/// </list>
/// </summary>
public sealed class ServiceUnderTest : IAsyncLifetime
{
    private const string LocalWriterKey = "e2e-writer-key";
    private const string LocalReaderKey = "e2e-reader-key";

    private Process? _process;
    private string? _dataDirectory;

    public Uri BaseAddress { get; private set; } = null!;

    public string WriterKey { get; private set; } = LocalWriterKey;

    public string ReaderKey { get; private set; } = LocalReaderKey;

    /// <summary>Leaderboard cache TTL of the target; tests wait this long before asserting on GET /leaderboard.</summary>
    public TimeSpan CacheTtl { get; } = TimeSpan.FromSeconds(1);

    public string Mode => _process is null ? $"external ({BaseAddress})" : $"local process ({BaseAddress})";

    public HttpClient CreateClient(string? apiKey)
    {
        var handler = new SocketsHttpHandler { MaxConnectionsPerServer = 256, PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
        var client = new HttpClient(handler) { BaseAddress = BaseAddress, Timeout = TimeSpan.FromSeconds(30) };
        if (apiKey is not null)
        {
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }

        return client;
    }

    public async Task InitializeAsync()
    {
        string? external = Environment.GetEnvironmentVariable("E2E_BASE_URL");
        if (!string.IsNullOrWhiteSpace(external))
        {
            BaseAddress = new Uri(external);
            WriterKey = Environment.GetEnvironmentVariable("E2E_WRITER_KEY") ?? LocalWriterKey;
            ReaderKey = Environment.GetEnvironmentVariable("E2E_READER_KEY") ?? LocalReaderKey;
        }
        else
        {
            StartLocalProcess();
        }

        await WaitUntilReadyAsync();
    }

    public Task DisposeAsync()
    {
        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            _process.WaitForExit(10_000);
        }

        _process?.Dispose();
        if (_dataDirectory is not null)
        {
            try
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Best effort: the OS may still hold the SQLite file for a moment after the process exits.
            }
        }

        return Task.CompletedTask;
    }

    private void StartLocalProcess()
    {
        int port = FreeTcpPort();
        BaseAddress = new Uri($"http://127.0.0.1:{port}");
        _dataDirectory = Path.Combine(Path.GetTempPath(), $"leaderboard-e2e-{Guid.NewGuid():N}");

        // The project reference copies Leaderboard.Api.dll (and its runtimeconfig) next to the test assembly.
        string apiDll = Path.Combine(AppContext.BaseDirectory, "Leaderboard.Api.dll");
        var start = new ProcessStartInfo("dotnet", $"\"{apiDll}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = AppContext.BaseDirectory,
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "E2E";
        start.Environment["ASPNETCORE_URLS"] = BaseAddress.ToString();
        start.Environment["ConnectionStrings__Leaderboard"] = $"Data Source={Path.Combine(_dataDirectory, "leaderboard.db")}";
        start.Environment["Auth__ApiKeys__0__Name"] = "e2e-writer";
        start.Environment["Auth__ApiKeys__0__Key"] = LocalWriterKey;
        start.Environment["Auth__ApiKeys__0__Roles__0"] = "scores:write";
        start.Environment["Auth__ApiKeys__0__Roles__1"] = "scores:read";
        start.Environment["Auth__ApiKeys__1__Name"] = "e2e-reader";
        start.Environment["Auth__ApiKeys__1__Key"] = LocalReaderKey;
        start.Environment["Auth__ApiKeys__1__Roles__0"] = "scores:read";
        start.Environment["RateLimit__PermitsPerSecond"] = "1000000";
        start.Environment["RateLimit__Burst"] = "1000000";
        start.Environment["Logging__LogLevel__Default"] = "Warning";

        _process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the API process.");
        _process.OutputDataReceived += (_, _) => { };
        _process.ErrorDataReceived += (_, _) => { };
        _process.BeginOutputReadLine(); // drain pipes so the child never blocks on a full buffer
        _process.BeginErrorReadLine();
    }

    private async Task WaitUntilReadyAsync()
    {
        using HttpClient client = CreateClient(apiKey: null);
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (_process is { HasExited: true })
            {
                throw new InvalidOperationException($"API process exited early with code {_process.ExitCode}.");
            }

            try
            {
                using HttpResponseMessage response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative));
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
                // not listening yet
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Service at {BaseAddress} was not ready within 30s.");
    }

    private static int FreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

[CollectionDefinition(Name)]
public sealed class E2EGroup : ICollectionFixture<ServiceUnderTest>
{
    public const string Name = "e2e";
}
