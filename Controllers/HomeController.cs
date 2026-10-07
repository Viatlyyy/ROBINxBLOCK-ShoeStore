using Microsoft.AspNetCore.Mvc;
using ShoeStore.Data;

namespace ShoeStore.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View(HomePageDemoData.Create());

    // Общая страница для ошибок вне Development; подробности исключения посетителю не передаём.
    public IActionResult Error() => View();
}
