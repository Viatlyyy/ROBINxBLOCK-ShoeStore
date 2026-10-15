using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Controllers;

public class CatalogController(ApplicationDbContext db) : Controller
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

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return NotFound();

        var product = await db.Products.AsNoTracking()
            .Include(item => item.Brand)
            .Include(item => item.Variants).ThenInclude(variant => variant.Sizes)
            .Include(item => item.Variants).ThenInclude(variant => variant.GalleryImages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.Id == id && item.Status == ProductStatus.Active, cancellationToken);

        return product is null ? NotFound() : View(product);
    }
}
