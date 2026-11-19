using Microsoft.AspNetCore.Mvc;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class CatalogController(ICatalogQueryService catalog) : Controller
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
    public IActionResult Details(int id)
    {
        var product = catalog.GetProduct(id);
        return product is null ? NotFound() : View(product);
    }
}
