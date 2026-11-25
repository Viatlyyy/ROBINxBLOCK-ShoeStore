using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

public class CartController(CartService cart, ICatalogQueryService catalog, CheckoutService checkout, ApplicationDbContext db) : Controller
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

    [HttpGet, Authorize]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId)) return await RequireCheckoutLoginAsync();

        var contacts = await db.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new
            {
                user.Email,
                user.PhoneNumber,
                Name = db.UserClaims.Where(claim => claim.UserId == user.Id && claim.ClaimType == ClaimTypes.GivenName)
                    .OrderBy(claim => claim.Id).Select(claim => claim.ClaimValue).FirstOrDefault()
            }).FirstOrDefaultAsync(cancellationToken);
        if (contacts is null) return await RequireCheckoutLoginAsync();

        var lines = await cart.GetLinesAsync(cancellationToken);
        if (lines.Count == 0) return RedirectToAction(nameof(Index));
        ViewData["Confirmation"] = cart.GetCheckoutId(lines, userId);
        return View(new CheckoutViewModel
        {
            Lines = lines,
            CustomerName = contacts.Name ?? "",
            Email = contacts.Email,
            Phone = contacts.PhoneNumber ?? ""
        });
    }

    [HttpPost, Authorize, ValidateAntiForgeryToken, EnableRateLimiting("checkout")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Checkout([Bind("CustomerName,LastName,Email,Phone,DeliveryMethod,City,Street,House,Apartment,Postcode,Comment,PaymentMethod")] CheckoutViewModel vm, Guid confirmation, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId) || !await db.Users.AnyAsync(user => user.Id == userId, cancellationToken))
            return await RequireCheckoutLoginAsync();

        var lines = await cart.GetLinesAsync(cancellationToken);
        vm.Lines = lines;
        if (lines.Count == 0)
        {
            TempData["Message"] = "Корзина пуста. Проверьте доступность товаров перед оформлением.";
            return RedirectToAction(nameof(Index));
        }
        var current = cart.GetCheckoutId(lines, userId);
        ViewData["Confirmation"] = current;
        if (confirmation != current)
            ModelState.AddModelError("", "Корзина изменилась. Проверьте состав и сумму перед подтверждением.");
        if (!ModelState.IsValid) return View(vm);
        var result = await checkout.PlaceOrderAsync(vm, lines, userId, confirmation, cancellationToken);
        if (result.RequiresSignIn) return await RequireCheckoutLoginAsync();
        if (!result.Success)
        {
            vm.Lines = await cart.GetLinesAsync(cancellationToken);
            ViewData["Confirmation"] = cart.GetCheckoutId(vm.Lines, userId);
            ModelState.AddModelError("", result.Error!);
            return View(vm);
        }
        cart.Clear();
        return RedirectToAction(nameof(Success), new { id = result.PublicId });
    }

    [HttpGet]
    public async Task<IActionResult> Success(Guid id, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(item => item.PublicId == id)
            .Select(item => new OrderSuccessViewModel { OrderNumber = item.Id, PublicId = item.PublicId })
            .FirstOrDefaultAsync(cancellationToken);
        return order is null ? NotFound() : View(order);
    }
    private async Task<IActionResult> RequireCheckoutLoginAsync()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return Challenge();
    }

    private bool IsAjax() => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private IActionResult AddFailure(string message)
    {
        if (IsAjax()) return BadRequest(new { message });
        TempData["Message"] = message;
        return RedirectToAction(nameof(Index));
    }
}
