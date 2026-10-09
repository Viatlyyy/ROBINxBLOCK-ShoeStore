using Microsoft.AspNetCore.Mvc;
using ShoeStore.Data;

namespace ShoeStore.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View(HomePageDemoData.Create());


    public IActionResult Error() => View();
}
