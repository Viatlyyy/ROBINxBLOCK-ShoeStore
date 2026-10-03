using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShoeStore.Data;

namespace ShoeStore.Tests.Integration;

// WebApplicationFactory запускает приложение Program на сервере внутри процесса тестов.
// В конструктор передаётся подключение именно к временной БД, созданной PostgreSqlFixture.
public sealed class RegistrationApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    // Переопределяем настройки запуска только для тестового экземпляра приложения.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Включаем среду разработки и указываем папку сайта, чтобы находились Razor-представления.
        builder.UseEnvironment("Development");
        builder.UseContentRoot(FindProjectDirectory());
        // Убираем логгеры тестового приложения, чтобы не засорять вывод тестов.
        builder.ConfigureLogging(logging => logging.ClearProviders());
        // Подставляем тестовую строку подключения в конфигурацию в памяти; файлы не изменяем.
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString }));
        // Заменяем регистрацию контекста БД в контейнере служб тестового приложения.
        builder.ConfigureTestServices(services =>
        {
            // Удаляем обычный контекст, его параметры и настройку, добавленную в Program.cs.
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            // Регистрируем контекст заново с PostgreSQL и нашим тестовым подключением.
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        });
    }

    internal static string FindProjectDirectory()
    {
        // Начинаем с папки запущенных тестов (bin/Debug/...) и поднимаемся к родительским папкам.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            // Наличие файла проекта приложения показывает, где расположен корень сайта.
            if (File.Exists(Path.Combine(directory.FullName, "ShoeStore.csproj")))
                return directory.FullName;
        }

        // Если папка не найдена, останавливаем запуск с явным сообщением о причине.
        throw new InvalidOperationException("Не найдена папка проекта ShoeStore.csproj для интеграционных тестов.");
    }
}
