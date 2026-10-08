using System.Data.Common;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Integration;

public sealed class HomePageApplicationFactory : WebApplicationFactory<Program>
{
    private readonly RejectDatabaseConnections databaseGuard = new();
    private readonly Mock<ICatalogQueryService> catalog = new(MockBehavior.Strict);
    public int DatabaseConnectionAttempts => databaseGuard.Attempts;
    public int CatalogRequestCount => catalog.Invocations.Count(call => call.Method.Name == nameof(ICatalogQueryService.GetCatalogAsync));
    public int RequestedCatalogPage => (int)catalog.Invocations.Single(call => call.Method.Name == nameof(ICatalogQueryService.GetCatalogAsync)).Arguments[0];
    public HomePageViewModel HomeModel { get; set; } = new()
    {
        Products =
        [
            new ProductCardViewModel
            {
                Id = 7001, Name = "Модель из базы", BrandName = "Тестовый бренд", Price = 12345m,
                ImageUrl = "/images/hero/9060-urban-feet-v6-960.webp"
            }
        ],
        Brands = HomePageDemoData.Create().Brands
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(FindWebProjectRoot());
        builder.UseSetting("ConnectionStrings:DefaultConnection",
            "Host=127.0.0.1;Port=1;Database=unavailable_homepage_tests;Username=homepage_tests;Timeout=1;Command Timeout=1");

        builder.ConfigureTestServices(services =>
        {
            catalog.Setup(service => service.GetHomeAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(HomeModel));
            catalog.Setup(service => service.GetCatalogAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .Returns((int page, CancellationToken token) => Task.FromResult(new CatalogPageViewModel
                {
                    Products = [], Page = 1, TotalPages = 1
                }));
            services.RemoveAll<ICatalogQueryService>();
            services.AddSingleton(catalog.Object);
            var roles = new Mock<RoleManager<IdentityRole>>(
                Mock.Of<IRoleStore<IdentityRole>>(), Array.Empty<IRoleValidator<IdentityRole>>(),
                new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
                NullLogger<RoleManager<IdentityRole>>.Instance);
            roles.Setup(manager => manager.RoleExistsAsync("Customer")).ReturnsAsync(true);
            services.RemoveAll<RoleManager<IdentityRole>>();
            services.AddSingleton(roles.Object);

            services.AddDbContext<ApplicationDbContext>(options => options.AddInterceptors(databaseGuard));
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
        });
    }

    private static string FindWebProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "ShoeStore.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException("Не найдена папка веб-проекта ShoeStore.");
    }

    private sealed class RejectDatabaseConnections : DbConnectionInterceptor
    {
        private int attempts;
        public int Attempts => Volatile.Read(ref attempts);

        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("В тестовом хосте запрещены реальные подключения к БД.");
        }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref attempts);
            throw new InvalidOperationException("В тестовом хосте запрещены реальные подключения к БД.");
        }
    }
}
