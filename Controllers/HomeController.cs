using Microsoft.AspNetCore.Mvc;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class HomeController(ICatalogQueryService catalog) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default) =>
        View(await catalog.GetHomeAsync(cancellationToken));

    // Общая страница для ошибок вне Development; подробности исключения посетителю не передаём.
    public IActionResult Error() => View();
}
