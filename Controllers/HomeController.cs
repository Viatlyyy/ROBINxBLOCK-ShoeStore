using Microsoft.AspNetCore.Mvc;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class HomeController(ICatalogQueryService catalog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default) =>
        View(await catalog.GetHomeAsync(cancellationToken));

    [HttpGet]
    public IActionResult Sort(string? tag, string? sort, string? search, int[]? brandIds) =>
        RedirectToAction(nameof(CatalogController.Index), "Catalog", new { tag, sort, search, brandIds });

    [HttpGet]
    public IActionResult SearchResults(string? tag, string? sort, string? search, int[]? brandIds) =>
        RedirectToAction(nameof(CatalogController.SearchResults), "Catalog", new { tag, sort, search, brandIds });

    // Общая страница для ошибок вне Development; подробности исключения посетителю не передаём.
    public IActionResult Error() => View();
}
