using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;

namespace ShoeStore.Tests.Integration;

// Настоящий MVC и настоящий PostgreSQL. Контроллер, Identity и Razor не подменяются.
public sealed class ProductDetailsApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    public static string ProjectRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "ShoeStore.csproj"))) return directory.FullName;
            throw new DirectoryNotFoundException("Не найден веб-проект ShoeStore.");
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(ProjectRoot);
        // Последний источник конфигурации заменяет подключение из любых пользовательских секретов.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connectionString }));
        builder.ConfigureTestServices(services =>
        {
            // Контекст создаётся с тестовым подключением напрямую: секреты рабочего сайта не используются.
            services.RemoveAll<ApplicationDbContext>();
            services.AddScoped(_ => new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString).Options));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }
}
