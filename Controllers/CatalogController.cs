using Microsoft.AspNetCore.Mvc;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class CatalogController(ICatalogQueryService catalog) : Controller
{
    public async Task<IActionResult> Index(
        int page = 1,
        CancellationToken cancellationToken = default) =>
        View(await catalog.GetCatalogAsync(page, cancellationToken));
}
