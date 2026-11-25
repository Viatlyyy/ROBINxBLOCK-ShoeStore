using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Services;

public sealed class FavoritesService(ApplicationDbContext db, ICatalogQueryService catalog, IHttpContextAccessor http)
{
    private Task<HashSet<int>>? favoriteIds;

    public Task<HashSet<int>> GetIdsAsync(CancellationToken cancellationToken)
    {
        var userId = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return favoriteIds ??= userId is null
            ? Task.FromResult(new HashSet<int>())
            : db.Favorites.Where(favorite => favorite.UserId == userId && db.Users.Any(user => user.Id == userId))
                .Select(favorite => favorite.ProductId).ToHashSetAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProductCardViewModel>> GetProductsAsync(string userId, int? limit, CancellationToken cancellationToken)
    {
        IQueryable<int> query = db.Favorites
            .Where(favorite => favorite.UserId == userId && favorite.Product.Status == ProductStatus.Active)
            .OrderByDescending(favorite => favorite.CreatedAt).ThenByDescending(favorite => favorite.Id)
            .Select(favorite => favorite.ProductId);
        if (limit is int count) query = query.Take(count);

        var ids = await query.ToListAsync(cancellationToken);
        return await catalog.GetProductsByIdsAsync(ids, cancellationToken);
    }

    public async Task<bool> SetAsync(string userId, int productId, bool isFavorite, CancellationToken cancellationToken)
    {
        if (!isFavorite)
        {
            await db.Favorites.Where(favorite => favorite.UserId == userId && favorite.ProductId == productId)
                .ExecuteDeleteAsync(cancellationToken);
            return true;
        }

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Favorites" ("UserId", "ProductId")
            SELECT {userId}, p."Id" FROM "Products" AS p
            WHERE p."Id" = {productId} AND p."Status" = {"Active"}
            ON CONFLICT ("UserId", "ProductId") DO NOTHING
            """, cancellationToken);
        return inserted > 0 || await db.Favorites.AnyAsync(favorite => favorite.UserId == userId
            && favorite.ProductId == productId && favorite.Product.Status == ProductStatus.Active, cancellationToken);
    }
}
