using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Services;

public sealed class CheckoutService(ApplicationDbContext db, IMemoryCache cache)
{
    private static readonly HashSet<string> DeliveryMethods = ["Курьер", "Самовывоз", "Пункт выдачи"];
    private static readonly HashSet<string> PaymentMethods = ["card", "apple", "google", "cash"];

    public async Task<CheckoutResult> PlaceOrderAsync(
        CheckoutViewModel model,
        IReadOnlyCollection<CartLine> lines,
        string? userId,
        Guid confirmation,
        CancellationToken cancellationToken)
    {
        if (lines.Count == 0) return CheckoutResult.Failed("Корзина пуста.");
        if (!DeliveryMethods.Contains(model.DeliveryMethod)) return CheckoutResult.Failed("Выберите доступный способ доставки.");
        if (!PaymentMethods.Contains(model.PaymentMethod)) return CheckoutResult.Failed("Выберите доступный способ оплаты.");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            if (await db.Orders.AnyAsync(order => order.PublicId == confirmation, cancellationToken))
                return CheckoutResult.Succeeded(confirmation);
            var remaining = new Dictionary<(int Product, int Variant, int Size), int>();
            foreach (var line in lines.OrderBy(line => line.Variant.Id).ThenBy(line => line.Size))
            {
                var stock = await db.Database.SqlQuery<int>($@"
                    UPDATE ""ProductVariantSizes"" AS s
                    SET ""StockQuantity"" = s.""StockQuantity"" - {line.Quantity}
                    FROM ""ProductVariants"" AS v
                    INNER JOIN ""Products"" AS p ON p.""Id"" = v.""ProductId""
                    WHERE s.""ProductVariantId"" = v.""Id""
                      AND s.""ProductVariantId"" = {line.Variant.Id}
                      AND s.""Size"" = {line.Size}
                      AND p.""Status"" = {"Active"}
                      AND p.""Price"" = {line.Product.Price}
                      AND s.""StockQuantity"" >= {line.Quantity}
                    RETURNING s.""StockQuantity"" AS ""Value""").ToListAsync(cancellationToken);
                if (stock.Count != 1)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return CheckoutResult.Failed($"Изменилась цена или доступный остаток: {line.Product.Name}, {line.Variant.Name}, размер {line.Size}.");
                }
                remaining[(line.Product.Id, line.Variant.Id, line.Size)] = stock[0];
            }

            var order = new Order
            {
                PublicId = confirmation,
                CustomerName = model.CustomerName.Trim(),
                LastName = Clean(model.LastName),
                Email = Clean(model.Email),
                Phone = model.Phone.Trim(),
                DeliveryMethod = model.DeliveryMethod,
                City = Clean(model.City),
                Street = Clean(model.Street),
                House = Clean(model.House),
                Apartment = Clean(model.Apartment),
                Postcode = Clean(model.Postcode),
                Comment = Clean(model.Comment),
                PaymentMethod = model.PaymentMethod,
                UserId = userId,
                Total = lines.Sum(line => line.Total),
                Items = lines.Select(line => new OrderItem
                {
                    ProductId = line.Product.Id,
                    ProductVariantId = line.Variant.Id,
                    ProductName = $"{line.Product.Brand.Name} · {line.Product.Name}",
                    VariantName = line.Variant.Name,
                    Sku = line.Variant.Sku,
                    ImageUrl = string.IsNullOrWhiteSpace(line.Variant.ImageUrl) ? line.Product.ImageUrl ?? "" : line.Variant.ImageUrl,
                    Size = line.Size,
                    Quantity = line.Quantity,
                    Price = line.Product.Price
                }).ToList()
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            CatalogQueryService.UpdateStock(cache, remaining);
            return CheckoutResult.Succeeded(order.PublicId);
        });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CheckoutResult(bool Success, Guid? PublicId, string? Error)
{
    public static CheckoutResult Succeeded(Guid publicId) => new(true, publicId, null);
    public static CheckoutResult Failed(string error) => new(false, null, error);
}
