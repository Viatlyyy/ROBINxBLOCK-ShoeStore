using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Controllers;

public class CatalogController(ApplicationDbContext db) : Controller
{
    public IActionResult Index() => RedirectToAction("Index", "Home", routeValues: null, fragment: "catalog");

    public IActionResult New()
    {
        ViewData["Title"] = "Новинки";
        return View("Collection", HomePageDemoData.Create().Products.Where(product => product.IsNew).ToList());
    }

    public IActionResult Hits()
    {
        ViewData["Title"] = "Хиты";
        return View("Collection", HomePageDemoData.Create().Products.Where(product => product.IsPopular).ToList());
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        // Несуществующий идентификатор сразу отклоняем, не открывая соединение с БД.
        if (id <= 0) return NotFound();

        // Карточка читает опубликованный товар и связанные данные, ничего не сохраняя.
        var product = await db.Products.AsNoTracking()
            .Include(item => item.Brand)
            .Include(item => item.Variants).ThenInclude(variant => variant.Sizes)
            .Include(item => item.Variants).ThenInclude(variant => variant.GalleryImages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.Id == id && item.Status == ProductStatus.Active, cancellationToken);
        return product is null ? NotFound() : View(product);
    }
}
