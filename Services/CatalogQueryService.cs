using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Services;

public sealed class CatalogQueryService(ApplicationDbContext db, IMemoryCache cache) : ICatalogQueryService
{
    public const int CatalogPageSize = 24;

    public async Task<HomePageViewModel> GetHomeAsync(CancellationToken cancellationToken)
    {
        var products = await LoadCardsAsync(db.Products.AsNoTracking()
            .Where(product => product.Status == ProductStatus.Active)
            .OrderByDescending(product => product.IsPopular)
            .ThenByDescending(product => product.IsNew)
            .ThenBy(product => product.Id), cancellationToken);

        return new HomePageViewModel
        {
            Products = products,
            Brands = HomePageDemoData.Create().Brands
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
            .ThenBy(product => product.Id)
            .Skip((page - 1) * CatalogPageSize)
            .Take(CatalogPageSize);

        var cards = await LoadCardsAsync(pageQuery, cancellationToken);
        return new CatalogPageViewModel
        {
            Products = cards,
            Page = page,
            TotalPages = totalPages
        };
    }

    public ProductCardViewModel? GetProduct(int id) =>
        cache.Get<ProductCardViewModel>((typeof(ProductCardViewModel), id));

    private async Task<List<ProductCardViewModel>> LoadCardsAsync(
        IQueryable<Product> query,
        CancellationToken cancellationToken)
    {
        var products = await query
            .Include(product => product.Brand)
            .Include(product => product.Variants.OrderByDescending(variant => variant.IsDefault)
                .ThenBy(variant => variant.Id).Take(1))
            .ThenInclude(variant => variant.GalleryImages)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var cards = products.Select(ToCard).ToList();
        foreach (var card in cards)
            cache.Set((typeof(ProductCardViewModel), card.Id), card);

        return cards;
    }

    private static ProductCardViewModel ToCard(Product product)
    {
        var variant = product.Variants.OrderByDescending(item => item.IsDefault).ThenBy(item => item.Id).FirstOrDefault();
        var mainImage = !string.IsNullOrWhiteSpace(variant?.ImageUrl) ? variant.ImageUrl : product.ImageUrl;
        var images = new List<string>();
        if (!string.IsNullOrWhiteSpace(mainImage)) images.Add(mainImage);
        if (variant is not null)
            images.AddRange(variant.GalleryImages.OrderBy(image => image.SortOrder).ThenBy(image => image.Id)
                .Select(image => image.ImageUrl));

        return new ProductCardViewModel
        {
            Id = product.Id,
            BrandId = product.BrandId,
            Name = product.Name,
            BrandName = product.Brand.Name,
            Description = product.Description,
            Price = product.Price,
            ImageUrl = product.ImageUrl ?? "",
            GalleryImageUrls = images.Where(url => !string.IsNullOrWhiteSpace(url))
                .Select(url => url.StartsWith("/images/gallery/", StringComparison.OrdinalIgnoreCase)
                    && url.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                        ? $"/images/gallery/optimized/{Path.GetFileNameWithoutExtension(url)}.jpg" : url)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            IsNew = product.IsNew,
            IsPopular = product.IsPopular
        };
    }

}
