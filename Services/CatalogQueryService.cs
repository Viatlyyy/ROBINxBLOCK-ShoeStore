using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Services;

public sealed class CatalogQueryService(ApplicationDbContext db) : ICatalogQueryService
{
    public const int CatalogPageSize = 24;

    public async Task<HomePageViewModel> GetHomeAsync(CancellationToken cancellationToken)
    {
        var products = await CardQuery()
            .OrderByDescending(product => product.IsPopular)
            .ThenByDescending(product => product.IsNew)
            .ThenBy(product => product.Id)
            .Take(16)
            .ToListAsync(cancellationToken);

        return new HomePageViewModel
        {
            Products = products,
            Brands = products.Select(product => product.BrandName).Distinct().Order().ToList()
        };
    }

    public async Task<CatalogPageViewModel> GetCatalogAsync(
        int page,
        CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking().Where(product => product.Status == ProductStatus.Active);

        var totalCount = await products.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)CatalogPageSize));
        page = Math.Clamp(page, 1, totalPages);
        var pageQuery = products
            .OrderByDescending(product => product.IsPopular)
            .ThenByDescending(product => product.IsNew)
            .ThenBy(product => product.Name)
            .Skip((page - 1) * CatalogPageSize)
            .Take(CatalogPageSize);

        var cards = await ToCards(pageQuery).ToListAsync(cancellationToken);
        return new CatalogPageViewModel
        {
            Products = cards,
            Page = page,
            TotalPages = totalPages
        };
    }

    public async Task<Product?> GetProductDetailsAsync(int id, CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking()
            .Include(product => product.Brand)
            .Include(product => product.Variants).ThenInclude(variant => variant.Sizes)
            .Include(product => product.Variants).ThenInclude(variant => variant.GalleryImages)
            .AsSplitQuery()
            .FirstOrDefaultAsync(product => product.Id == id && product.Status == ProductStatus.Active, cancellationToken);

    private IQueryable<ProductCardViewModel> CardQuery() => ToCards(
        db.Products.AsNoTracking().Where(product => product.Status == ProductStatus.Active));

    private static IQueryable<ProductCardViewModel> ToCards(IQueryable<Product> query) => query.Select(product => new ProductCardViewModel
    {
        Id = product.Id,
        BrandId = product.BrandId,
        Name = product.Name,
        BrandName = product.Brand.Name,
        Price = product.Price,
        IsNew = product.IsNew,
        IsPopular = product.IsPopular,
        Variants = product.Variants
            .OrderByDescending(variant => variant.IsDefault)
            .ThenBy(variant => variant.Id)
            .Select(variant => new ProductCardVariantViewModel
            {
                Id = variant.Id,
                Name = variant.Name,
                Swatch = variant.Swatch,
                ImageUrl = variant.ImageUrl,
                ThumbnailUrl = variant.ThumbnailUrl,
                IsDefault = variant.IsDefault
            })
            .ToList()
    });

}
