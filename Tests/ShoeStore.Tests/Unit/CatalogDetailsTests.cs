using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShoeStore.Controllers;
using ShoeStore.Data;

namespace ShoeStore.Tests.Unit;

[TestClass]
public class CatalogDetailsTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public async Task Details_InvalidId_ReturnsNotFoundBeforeOpeningDatabase(int id)
    {
        var guard = new DatabaseAccessGuard();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1")
            .AddInterceptors(guard).Options;
        await using var db = new ApplicationDbContext(options);

        var result = await new CatalogController(db).Details(id);

        Assert.IsInstanceOfType<NotFoundResult>(result);
        Assert.AreEqual(0, guard.OpenAttempts, "Некорректный ID не должен открывать соединение.");
    }

    // Настоящий контекст и контроллер; перехватчик запрещает любой доступ к БД.
    private sealed class DatabaseAccessGuard : DbConnectionInterceptor
    {
        public int OpenAttempts { get; private set; }

        public override InterceptionResult ConnectionOpening(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result)
        {
            OpenAttempts++;
            throw new AssertFailedException("Details попытался обратиться к БД с некорректным ID.");
        }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            OpenAttempts++;
            throw new AssertFailedException("Details попытался обратиться к БД с некорректным ID.");
        }
    }
}
