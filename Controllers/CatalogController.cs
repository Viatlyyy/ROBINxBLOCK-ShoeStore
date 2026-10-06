using Microsoft.AspNetCore.Mvc;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class CatalogController(ICatalogQueryService catalog) : Controller
{
    public async Task<IActionResult> Index(
        int page = 1,
        CancellationToken cancellationToken = default) =>
        View(await catalog.GetCatalogAsync(page, cancellationToken));

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        var product = await catalog.GetProductDetailsAsync(id, cancellationToken);
        return product is null ? NotFound() : View(product);
    }
}
