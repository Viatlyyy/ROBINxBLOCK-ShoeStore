using Microsoft.AspNetCore.Mvc;

namespace ShoeStore.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Error() => View();
}
