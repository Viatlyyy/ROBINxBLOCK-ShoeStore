using Microsoft.AspNetCore.Mvc;

namespace ShoeStore.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    // Общая страница для ошибок вне Development; подробности исключения посетителю не передаём.
    public IActionResult Error() => View();
}
