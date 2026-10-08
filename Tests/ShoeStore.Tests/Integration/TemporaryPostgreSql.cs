using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;

namespace ShoeStore.Tests.Integration;

public sealed class TemporaryPostgreSql : IAsyncDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "shoestore-card-tests-" + Guid.NewGuid().ToString("N"));
    private readonly string binDirectory;
    private bool serverStarted;
    public string ConnectionString { get; }

    private TemporaryPostgreSql()
    {
        binDirectory = FindPostgreSql();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        ConnectionString = $"Host=127.0.0.1;Port={port};Database=postgres;Username=card_tests;Timeout=5;Command Timeout=30";
    }

    public static async Task<TemporaryPostgreSql> StartAsync()
    {
        var database = new TemporaryPostgreSql();
        try
        {
            Directory.CreateDirectory(database.directory);
            await database.RunAsync("initdb", "-D", database.directory, "-U", "card_tests", "-A", "trust", "-E", "UTF8", "--no-locale");
            var port = new NpgsqlConnectionStringBuilder(database.ConnectionString).Port;
            await database.RunAsync("pg_ctl", "-D", database.directory, "-l", Path.Combine(database.directory, "postgres.log"),
                "-o", $"-h 127.0.0.1 -p {port}", "-w", "-t", "20", "start");
            database.serverStarted = true;

            await using var connection = new NpgsqlConnection(database.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SHOW data_directory", connection);
            var actual = (string)(await command.ExecuteScalarAsync())!;
            if (!Path.GetFullPath(actual).Equals(Path.GetFullPath(database.directory), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Тест подключился не к своему временному серверу.");
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    public async Task ImportSchemaAsync(string schemaPath)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(await File.ReadAllTextAsync(schemaPath), connection);
        await command.ExecuteNonQueryAsync();
        await using var searchPath = new NpgsqlCommand("SET search_path TO public", connection);
        await searchPath.ExecuteNonQueryAsync();
        await connection.ReloadTypesAsync();
    }

    public async Task<string> CatalogSnapshotAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        var rows = new List<string>();
        foreach (var table in new[] { "Products", "Brands", "Categories", "ProductVariants", "ProductVariantImages", "ProductVariantSizes" })
        {
            await using var command = new NpgsqlCommand($"SELECT COALESCE(string_agg(j, E'\\n' ORDER BY j), '') FROM (SELECT to_jsonb(t)::text j FROM public.\"{table}\" t) data", connection);
            rows.Add(table + ":" + await command.ExecuteScalarAsync());
        }
        return string.Join("\n", rows);
    }

    public async ValueTask DisposeAsync()
    {
        if (!Directory.Exists(directory)) return;
        if (serverStarted || File.Exists(Path.Combine(directory, "postmaster.pid")))
        {
            await RunAsync("pg_ctl", "-D", directory, "-m", "fast", "-w", "-t", "20", "stop");
            serverStarted = false;
        }
        using var poolKey = new NpgsqlConnection(ConnectionString);
        NpgsqlConnection.ClearPool(poolKey);

        var root = Path.GetFullPath(directory);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var name = Path.GetFileName(root);
        if (!root.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
            || !name.StartsWith("shoestore-card-tests-", StringComparison.Ordinal)
            || !Guid.TryParseExact(name["shoestore-card-tests-".Length..], "N", out _))
            throw new InvalidOperationException("Отказ очистки папки вне временного каталога теста.");
        Directory.Delete(root, recursive: true);
    }

    private async Task RunAsync(string executable, params string[] arguments)
    {
        var startsServer = executable == "pg_ctl" && arguments.Contains("start");
        var info = new ProcessStartInfo(Path.Combine(binDirectory, executable + (OperatingSystem.IsWindows() ? ".exe" : "")))
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = !startsServer, RedirectStandardError = !startsServer
        };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Не удалось запустить PostgreSQL для теста.");
        var output = startsServer ? Task.FromResult("") : process.StandardOutput.ReadToEndAsync();
        var error = startsServer ? Task.FromResult("") : process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw new TimeoutException($"Превышено время запуска {executable}.");
        }
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Ошибка {executable}: {await output}\n{await error}");
        await output;
        await error;
    }

    private static string FindPostgreSql()
    {
        var configured = Environment.GetEnvironmentVariable("POSTGRES_BIN");
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(configured)) candidates.Add(configured);
        var installed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PostgreSQL");
        if (Directory.Exists(installed))
            candidates.AddRange(Directory.GetDirectories(installed).OrderByDescending(path => Path.GetFileName(path)).Select(path => Path.Combine(path, "bin")));
        var extension = OperatingSystem.IsWindows() ? ".exe" : "";
        var result = candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "initdb" + extension)) && File.Exists(Path.Combine(path, "pg_ctl" + extension)));
        return result ?? throw new DirectoryNotFoundException("Для интеграционных тестов установите PostgreSQL. Для нестандартной папки задайте POSTGRES_BIN с путём к bin.");
    }
}

