using Microsoft.AspNetCore.Mvc;

namespace ShoeStore.Controllers;

public class CatalogController : Controller
{
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
