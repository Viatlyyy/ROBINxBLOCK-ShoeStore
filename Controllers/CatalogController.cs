using Microsoft.AspNetCore.Mvc;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class CatalogController(ICatalogQueryService catalog) : Controller
{
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default) =>
        View(await catalog.GetCatalogAsync(page, cancellationToken));

    public IActionResult New()
    {
        ViewData["Title"] = "Новинки";
        return View("Collection");
    }

    public IActionResult Hits()
    {
        ViewData["Title"] = "Хиты";
        return View("Collection");
    }
}
