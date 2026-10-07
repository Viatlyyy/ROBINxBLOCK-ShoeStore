using Microsoft.AspNetCore.Mvc;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class CatalogController : Controller
{
    private readonly ICatalogQueryService catalog;

    public CatalogController(ICatalogQueryService catalog) => this.catalog = catalog;

    public IActionResult Index() => RedirectToAction("Index", "Home", routeValues: null, fragment: "catalog");

    public async Task<IActionResult> New(CancellationToken cancellationToken = default)
    {
        var model = await catalog.GetHomeAsync(cancellationToken);
        ViewData["Title"] = "Новинки";
        return View("Collection", model.Products.Where(product => product.IsNew).ToList());
    }

    public async Task<IActionResult> Hits(CancellationToken cancellationToken = default)
    {
        var model = await catalog.GetHomeAsync(cancellationToken);
        ViewData["Title"] = "Хиты";
        return View("Collection", model.Products.Where(product => product.IsPopular).ToList());
    }
}
