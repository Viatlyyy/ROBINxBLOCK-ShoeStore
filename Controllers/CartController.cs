using Microsoft.AspNetCore.Mvc;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class CartController(CartService cart, ICatalogQueryService catalog) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var lines = await cart.GetLinesAsync(cancellationToken);
        var selected = lines.Select(line => line.Product.Id).ToHashSet();
        var home = await catalog.GetHomeAsync(cancellationToken);
        var recommendations = home.Products.Where(product => !selected.Contains(product.Id))
            .OrderByDescending(product => product.IsPopular).ThenByDescending(product => product.IsNew)
            .ThenBy(product => product.Id).Take(4).ToList();
        return View(new CartPageViewModel { Lines = lines, Recommendations = recommendations });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int variantId, int size, int quantity = 1, CancellationToken cancellationToken = default)
    {
        var result = await cart.AddAsync(variantId, size, quantity, cancellationToken);
        if (!result.Success) return AddFailure(result.Error!);
        if (IsAjax()) return Json(new { count = result.Count });
        TempData["Message"] = "Модель добавлена в корзину";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public IActionResult Remove(int variantId, int size)
    {
        cart.Remove(variantId, size);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeQuantity(int variantId, int size, int change, CancellationToken cancellationToken)
    {
        if (change is not (-1 or 1)) return BadRequest();
        await cart.ChangeQuantityAsync(variantId, size, change, cancellationToken);
        if (!IsAjax()) return RedirectToAction(nameof(Index));
        var lines = await cart.GetLinesAsync(cancellationToken);
        var line = lines.FirstOrDefault(x => x.Variant.Id == variantId && x.Size == size);
        return Json(new { lineCount = lines.Count, quantity = line?.Quantity ?? 0, lineTotal = line?.Total ?? 0, total = lines.Sum(x => x.Total), count = lines.Sum(x => x.Quantity) });
    }

    private bool IsAjax() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private IActionResult AddFailure(string message)
    {
        if (IsAjax()) return BadRequest(new { message });
        TempData["Message"] = message;
        return RedirectToAction(nameof(Index));
    }
}
