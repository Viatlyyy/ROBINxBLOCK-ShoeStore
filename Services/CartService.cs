using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Services;

public sealed class CartService(ApplicationDbContext db, IHttpContextAccessor httpContextAccessor)
{
    private const string SessionKey = "cart";
    public const int MaxLines = 10;
    public const int MaxQuantity = 10;

    private ISession Session => httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("Сессия корзины недоступна.");

    public static int GetCount(ISession session, bool initialize)
    {
        if (initialize && session.GetString(SessionKey) is null)
            session.SetString(SessionKey, "[]");
        return Read(session).Sum(item => item.Quantity);
    }

    private static List<CartItem> Read(ISession session)
    {
        try
        {
            return (JsonSerializer.Deserialize<List<CartItem>>(session.GetString(SessionKey) ?? "[]") ?? [])
                .Where(item => item.ProductVariantId > 0 && item.Size is >= 30 and <= 50)
                .GroupBy(item => new { item.ProductVariantId, item.Size })
                .Take(MaxLines)
                .Select(group => new CartItem
                {
                    ProductVariantId = group.Key.ProductVariantId,
                    Size = group.Key.Size,
                    Quantity = Math.Clamp(group.Sum(item => item.Quantity), 1, MaxQuantity)
                })
                .ToList();
        }
        catch (JsonException)
        {
            session.Remove(SessionKey);
            return [];
        }
    }

    private void Save(IEnumerable<CartItem> items) =>
        Session.SetString(SessionKey, JsonSerializer.Serialize(items.Take(MaxLines)));


    public void Clear()
    {
        Session.Remove(SessionKey);
        Session.Remove("checkout-id");
        Session.Remove("checkout-user");
        Session.Remove("checkout-lines");
    }

    public Guid GetCheckoutId(IEnumerable<CartLine> lines, string userId)
    {
        var snapshot = string.Join(";", lines.OrderBy(line => line.Variant.Id).ThenBy(line => line.Size)
            .Select(line => FormattableString.Invariant($"{line.Variant.Id}:{line.Size}:{line.Quantity}:{line.Product.Price}")));
        if (Session.GetString("checkout-user") == userId && Session.GetString("checkout-lines") == snapshot &&
            Guid.TryParse(Session.GetString("checkout-id"), out var current)) return current;

        var id = Guid.NewGuid();
        Session.SetString("checkout-user", userId);
        Session.SetString("checkout-lines", snapshot);
        Session.SetString("checkout-id", id.ToString());
        return id;
    }

    public async Task<List<CartLine>> GetLinesAsync(CancellationToken cancellationToken = default)
    {
        var cart = Read(Session);
        if (cart.Count == 0) return [];
        var ids = cart.Select(item => item.ProductVariantId).Distinct().ToList();
        var variants = await db.ProductVariants.AsNoTracking()
            .Include(variant => variant.Product).ThenInclude(product => product.Brand)
            .Include(variant => variant.Sizes)
            .Where(variant => ids.Contains(variant.Id) && variant.Product.Status == ProductStatus.Active)
            .ToListAsync(cancellationToken);

        var lines = cart.Join(
                variants,
                item => item.ProductVariantId,
                variant => variant.Id,
                (item, variant) => new CartLine { Variant = variant, Size = item.Size, Quantity = item.Quantity })
            .Where(line => line.Variant.Sizes.Any(size => size.Size == line.Size && size.StockQuantity > 0))
            .Select(line =>
            {
                var stock = line.Variant.Sizes.First(size => size.Size == line.Size).StockQuantity;
                line.Quantity = Math.Min(line.Quantity, Math.Min(stock, MaxQuantity));
                return line;
            })
            .ToList();

        var normalized = lines.Select(line => new CartItem
        {
            ProductVariantId = line.Variant.Id,
            Size = line.Size,
            Quantity = line.Quantity
        }).ToList();
        if (normalized.Count != cart.Count || normalized.Where((item, index) => item.Quantity != cart[index].Quantity).Any()) Save(normalized);
        return lines;
    }

    public async Task<CartMutationResult> AddAsync(int variantId, int size, int quantity, CancellationToken cancellationToken)
    {
        if (quantity is < 1 or > MaxQuantity) return CartMutationResult.Failed("Выберите количество от 1 до 10.");
        var stock = await db.ProductVariantSizes.AsNoTracking()
            .Where(item => item.ProductVariantId == variantId
                && item.Size == size
                && item.ProductVariant.Product.Status == ProductStatus.Active)
            .Select(item => item.StockQuantity)
            .FirstOrDefaultAsync(cancellationToken);
        if (stock <= 0) return CartMutationResult.Failed("Выбранный размер больше недоступен.");

        await GetLinesAsync(cancellationToken);
        var cart = Read(Session);
        var item = cart.FirstOrDefault(entry => entry.ProductVariantId == variantId && entry.Size == size);
        if (item is null)
        {
            if (cart.Count >= MaxLines) return CartMutationResult.Failed($"В корзине может быть не больше {MaxLines} разных позиций.");
            cart.Add(new CartItem { ProductVariantId = variantId, Size = size, Quantity = Math.Min(quantity, stock) });
        }
        else
        {
            var maximum = Math.Min(stock, MaxQuantity);
            if (item.Quantity >= maximum)
                return CartMutationResult.Failed("В корзине уже доступный максимум этого размера.");
            item.Quantity = Math.Min(item.Quantity + quantity, maximum);
        }
        Save(cart);
        return CartMutationResult.Succeeded(cart.Sum(entry => entry.Quantity));
    }

    public void Remove(int variantId, int size)
    {
        var cart = Read(Session);
        cart.RemoveAll(item => item.ProductVariantId == variantId && item.Size == size);
        Save(cart);
    }

    public async Task ChangeQuantityAsync(int variantId, int size, int change, CancellationToken cancellationToken)
    {
        var cart = Read(Session);
        var item = cart.FirstOrDefault(entry => entry.ProductVariantId == variantId && entry.Size == size);
        if (item is null) return;
        var stock = await db.ProductVariantSizes.AsNoTracking()
            .Where(entry => entry.ProductVariantId == variantId && entry.Size == size && entry.ProductVariant.Product.Status == ProductStatus.Active)
            .Select(entry => entry.StockQuantity)
            .FirstOrDefaultAsync(cancellationToken);
        if (stock <= 0)
        {
            cart.Remove(item);
        }
        else
        {
            item.Quantity = Math.Clamp(item.Quantity + Math.Sign(change), 1, Math.Min(stock, MaxQuantity));
        }
        Save(cart);
    }
}

public sealed record CartMutationResult(bool Success, int Count, string? Error)
{
    public static CartMutationResult Succeeded(int count) => new(true, count, null);
    public static CartMutationResult Failed(string error) => new(false, 0, error);
}
