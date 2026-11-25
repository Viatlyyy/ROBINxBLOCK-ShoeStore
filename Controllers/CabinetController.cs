using System.Data.Common;
using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Controllers;

[Authorize]
[Route("Account")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CabinetController(ApplicationDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, FavoritesService favorites, ILogger<CabinetController> logger) : Controller
{
    private const int OrdersPerPage = 20;
    private ApplicationUser account = null!;

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
        {
            await signIn.SignOutAsync();
            context.Result = Challenge();
            return;
        }

        account = user;
        await next();
    }

    private static readonly Expression<Func<Order, AccountOrderSummary>> OrderSummary = order =>
        new AccountOrderSummary(order.PublicId, order.Id, order.CreatedAt, order.Status, order.Total,
            order.Items.OrderBy(line => line.Id).Select(line => line.ImageUrl).FirstOrDefault());

    private IQueryable<Order> OwnOrders()
    {
        var userId = account.Id;
        return db.Orders.AsNoTracking().Where(order => order.UserId == userId)
            .OrderByDescending(order => order.CreatedAt).ThenByDescending(order => order.Id);
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {

        var latest = await OwnOrders().Select(OrderSummary).FirstOrDefaultAsync(cancellationToken);
        var saved = await favorites.GetProductsAsync(account.Id, 2, cancellationToken);
        return View(new AccountOverviewViewModel(await ReadProfileAsync(account), latest, saved));
    }

    [HttpGet("Profile")]
    public async Task<IActionResult> Profile()
    {
        return View(await ReadProfileAsync(account));
    }

    [HttpPost("Profile"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile([Bind("DisplayName,Phone")] CustomerProfileViewModel model, CancellationToken cancellationToken)
    {
        model.Email = account.Email;
        if (!ModelState.IsValid) return View(model);

        var name = model.DisplayName.Trim();
        var phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone.Trim();
        IdentityResult result;
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var nameClaim = (await users.GetClaimsAsync(account)).FirstOrDefault(claim => claim.Type == ClaimTypes.GivenName);
            var updatedClaim = new Claim(ClaimTypes.GivenName, name);
            result = nameClaim is null ? await users.AddClaimAsync(account, updatedClaim)
                : nameClaim.Value != name ? await users.ReplaceClaimAsync(account, nameClaim, updatedClaim) : IdentityResult.Success;

            if (result.Succeeded && account.PhoneNumber != phone)
                result = await users.SetPhoneNumberAsync(account, phone);
            if (result.Succeeded) await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception error) when (error is DbUpdateException or DbException)
        {
            logger.LogWarning("Не удалось сохранить профиль. Тип ошибки {ErrorType}.", error.GetType().Name);
            result = IdentityResult.Failed();
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Не удалось сохранить контакты. Повторите попытку.");
            return View(model);
        }

        await signIn.RefreshSignInAsync(account);
        TempData["ProfileSaved"] = "Контактные данные сохранены.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("Orders")]
    public async Task<IActionResult> Orders(int page = 1, CancellationToken cancellationToken = default)
    {
        var query = OwnOrders();
        var count = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(count / (double)OrdersPerPage));
        page = Math.Clamp(page, 1, totalPages);
        var orders = await query.Skip((page - 1) * OrdersPerPage).Take(OrdersPerPage)
            .Select(OrderSummary).ToListAsync(cancellationToken);
        return View(new AccountOrdersViewModel(orders, page, totalPages));
    }

    [HttpGet("Orders/{publicId:guid}")]
    public async Task<IActionResult> Order(Guid publicId, CancellationToken cancellationToken)
    {
        var order = await OwnOrders().Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.PublicId == publicId, cancellationToken);
        if (order is null) return NotFound();

        var lines = order.Items.OrderBy(item => item.Id).Select(item => new AccountOrderLine(
            item.ProductName, item.VariantName, item.Size, item.Quantity, item.Price, item.ImageUrl)).ToArray();
        var summary = new AccountOrderSummary(order.PublicId, order.Id, order.CreatedAt, order.Status, order.Total, lines.FirstOrDefault()?.ImageUrl);
        var address = string.Join(", ", new[] { order.Postcode, order.City, order.Street, order.House, order.Apartment }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return View(new AccountOrderDetails(summary, order.DeliveryMethod, address, order.PaymentMethod, lines));
    }

    [HttpGet("Favorites")]
    public async Task<IActionResult> Favorites(CancellationToken cancellationToken)
    {
        return View(await favorites.GetProductsAsync(account.Id, null, cancellationToken));
    }

    [HttpPost("Favorites"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetFavorite(int productId, bool? isFavorite, string? returnUrl, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || productId <= 0 || isFavorite is null) return BadRequest();

        var ajax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        if (!await favorites.SetAsync(account.Id, productId, isFavorite.Value, cancellationToken))
        {
            const string message = "Товар сейчас недоступен.";
            if (ajax) return NotFound(new { message });
            TempData["FavoriteNotice"] = message;
        }
        else if (ajax)
        {
            return Json(new { productId, isFavorite });
        }

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction(nameof(Favorites));
    }

    private async Task<CustomerProfileViewModel> ReadProfileAsync(ApplicationUser user) => new()
    {
        DisplayName = (await users.GetClaimsAsync(user)).FirstOrDefault(claim => claim.Type == ClaimTypes.GivenName)?.Value ?? "",
        Email = user.Email,
        Phone = user.PhoneNumber
    };
}
