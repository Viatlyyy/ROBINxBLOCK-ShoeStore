using System.Text.RegularExpressions;
using Npgsql;

namespace ShoeStore.Tests.Integration;


public sealed class PostgreSqlFixture
{
    // Имя переменной окружения, из которой берём параметры сервера, пользователя и пароль.
    public const string ConnectionVariable = "SHOESTORE_TEST_POSTGRES";
    // Новое случайное имя при каждом запуске защищает от смешивания тестовых данных.
    private readonly string databaseName = $"shoestore_registration_tests_{Guid.NewGuid():N}";
    // Служебное подключение к postgres используется для команд создания и удаления БД.
    private string adminConnectionString = "";
    // Флаг разрешает удаление только после успешного создания нашей временной БД.
    private bool databaseCreated;
    // После подготовки через Factory можно получить тестовый HTTP-клиент и службы приложения.
    public RegistrationApplicationFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionVariable);
        // При отсутствии подключения подготовку пропускаем; сам тест отметит это как Inconclusive.
        if (string.IsNullOrWhiteSpace(configured)) return;

        // Сохраняем параметры доступа к серверу, но заменяем имя базы на служебную postgres.
        // База, указанная пользователем в строке, не становится целью этих тестов.
        var connection = new NpgsqlConnectionStringBuilder(configured)
        {
            Database = "postgres",
            SearchPath = "public",
            // Не сохраняем соединения в пуле, чтобы они не удерживали временную БД после тестов.
            Pooling = false,
            // Ограничения ожидания подключения и SQL-команды, в секундах.
            Timeout = 10,
            CommandTimeout = 30
        };
        adminConnectionString = connection.ConnectionString;
        try
        {
            // Для CREATE DATABASE нужны соответствующие права PostgreSQL у тестового пользователя.
            await ExecuteAdminAsync($"CREATE DATABASE {QuotedDatabaseName()}");
            databaseCreated = true;
            // Все дальнейшие операции с таблицами направляем только в созданную временную БД.
            connection.Database = databaseName;
            // Импортируем тот же SQL-файл, который другой пользователь применяет через pgAdmin.
            // SQL выполняется только в нашей новой временной БД, не в существующей базе магазина.
            var schemaPath = Path.Combine(AppContext.BaseDirectory, "Database", "shoestore-schema.sql");
            var schema = await File.ReadAllTextAsync(schemaPath);
            await using (var database = new NpgsqlConnection(connection.ConnectionString))
            {
                await database.OpenAsync();
                await using var command = new NpgsqlCommand(schema, database);
                await command.ExecuteNonQueryAsync();
            }

            // Передаём временное подключение фабрике тестового приложения.
            Factory = new RegistrationApplicationFactory(connection.ConnectionString);
        }
        catch
        {
            // Если подготовка сломалась, освобождаем уже созданные ресурсы и не скрываем ошибку.
            await DisposeAsync();
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        // Сначала останавливаем тестовое приложение, затем удаляем его временную БД.
        if (Factory is not null) await Factory.DisposeAsync();
        if (!databaseCreated) return;

        // FORCE завершает оставшиеся подключения.
        await ExecuteAdminAsync($"DROP DATABASE {QuotedDatabaseName()} WITH (FORCE)");
        // Повторное освобождение не должно пытаться удалить эту БД ещё раз.
        databaseCreated = false;
    }

    private string QuotedDatabaseName()
    {
        // Проверяем точный префикс и 32 шестнадцатеричных символа, чтобы не затронуть другую БД.
        if (!Regex.IsMatch(databaseName, "^shoestore_registration_tests_[0-9a-f]{32}$"))
            throw new InvalidOperationException("Недопустимое имя временной тестовой базы.");
        // QuoteIdentifier безопасно оформляет имя БД как SQL-идентификатор в двойных кавычках.
        return new NpgsqlCommandBuilder().QuoteIdentifier(databaseName);
    }

    private async Task ExecuteAdminAsync(string sql)
    {
        // Открываем служебное подключение, выполняем команду и освобождаем оба объекта.
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
