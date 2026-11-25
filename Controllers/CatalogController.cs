using Microsoft.AspNetCore.Mvc;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class CatalogController(ICatalogQueryService catalog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(string? tag = null, string? sort = null, string? search = null,
        int[]? brandIds = null, CancellationToken cancellationToken = default)
    {
        var selection = CatalogSelection.FromQuery(tag, sort, search, brandIds);
        ViewData["ShowProductBadges"] = true;
        return View("Index", await catalog.GetHomeAsync(selection, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> SearchResults(string? tag = null, string? sort = null, string? search = null,
        int[]? brandIds = null, CancellationToken cancellationToken = default)
    {
        var selection = CatalogSelection.FromQuery(tag, sort, search, brandIds);
        ViewData["ShowProductBadges"] = true;
        return PartialView("_CatalogProducts", await catalog.GetHomeAsync(selection, cancellationToken));
    }

    [HttpGet]
    public IActionResult New(string? sort, string? search, int[]? brandIds) =>
        RedirectToAction(nameof(Index), new { tag = "new", sort, search, brandIds });

    [HttpGet]
    public IActionResult Hits(string? sort, string? search, int[]? brandIds) =>
        RedirectToAction(nameof(Index), new { tag = "popular", sort, search, brandIds });

    [HttpGet]
    public async Task<IActionResult> Details(int id, int? variantId = null, CancellationToken cancellationToken = default)
    {
        var product = await catalog.GetProductAsync(id, cancellationToken);
        if (product is null) return NotFound();

        ViewData["SelectedVariantId"] = variantId;
        return View(product);
    }
}
