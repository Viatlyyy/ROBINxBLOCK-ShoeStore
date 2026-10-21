using Microsoft.AspNetCore.Mvc;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class HomeController(ICatalogQueryService catalog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default) =>
        View(await catalog.GetHomeAsync(cancellationToken));

    [HttpGet]
    public async Task<IActionResult> Sort(string? tag = null, string? sort = null, CancellationToken cancellationToken = default)
    {
        var selection = ShoeStore.Models.CatalogSelection.FromQuery(tag, sort);
        var model = await catalog.GetHomeAsync(selection, cancellationToken);
        ViewData["IsHomePage"] = true;
        return View("Index", model);
    }


    // Общая страница для ошибок вне Development; подробности исключения посетителю не передаём.
    public IActionResult Error() => View();
}
