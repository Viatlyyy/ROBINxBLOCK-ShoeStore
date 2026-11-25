using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Services;

public sealed class CatalogQueryService(ApplicationDbContext db, IMemoryCache cache) : ICatalogQueryService
{
    public const int CatalogPageSize = 24;

    public async Task<HomePageViewModel> GetHomeAsync(CatalogSelection selection, CancellationToken cancellationToken)
    {
        var cards = await GetPublishedCardsAsync(cancellationToken);
        return new HomePageViewModel
        {
            Products = ProductQuery.ApplySelection(cards, selection).ToList(),
            Brands = HomePageDemoData.Create().Brands,
            CatalogBrands = cards.GroupBy(card => card.BrandId)
                .OrderBy(brand => brand.First().BrandName)
                .ThenBy(brand => brand.Key)
                .ToDictionary(brand => brand.Key, brand => brand.First().BrandName),
            Selection = selection
        };
    }

    public Task<HomePageViewModel> GetHomeAsync(CancellationToken cancellationToken) =>
        GetHomeAsync(CatalogSelection.FromQuery(null, null), cancellationToken);

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

    private static readonly object StockUpdateLock = new();
    private static long stockVersion;
    private static readonly object PublishedKey = (typeof(ProductCardViewModel), "published");

    internal static void UpdateStock(IMemoryCache cache, Dictionary<(int Product, int Variant, int Size), int> remaining)
    {
        lock (StockUpdateLock)
        {
            stockVersion++;
            var updated = new Dictionary<int, ProductCardViewModel>();
            foreach (var id in remaining.Keys.Select(key => key.Product).Distinct())
            {
                var key = (typeof(ProductCardViewModel), id);
                if (cache.Get<ProductCardViewModel>(key) is not { } card) continue;
                var sizes = card.Sizes.Select(size => remaining.TryGetValue((id, card.VariantId, size.Size), out var stock)
                    ? size with { StockQuantity = Math.Min(size.StockQuantity, stock) } : size)
                    .Where(size => size.StockQuantity > 0).ToArray();
                updated[id] = cache.Set(key, card with { Sizes = sizes });
            }
            if (cache.Get<List<ProductCardViewModel>>(PublishedKey) is { } published)
                cache.Set(PublishedKey, published.Select(card => updated.GetValueOrDefault(card.Id, card)).ToList());
        }
    }

    private Task<List<ProductCardViewModel>> GetPublishedCardsAsync(CancellationToken cancellationToken)
    {
        if (cache.Get<List<ProductCardViewModel>>(PublishedKey) is { } cards) return Task.FromResult(cards);
        return LoadCardsAsync(db.Products.AsNoTracking()
            .Where(product => product.Status == ProductStatus.Active)
            .OrderBy(product => product.Id), cancellationToken, publishList: true);
    }

    private async Task<List<ProductCardViewModel>> LoadCardsAsync(
        IQueryable<Product> query,
        CancellationToken cancellationToken,
        bool publishList = false)
    {
        while (true)
        {
            var version = Volatile.Read(ref stockVersion);
            var products = await query
                .Include(product => product.Brand)
                .Include(product => product.Variants.OrderByDescending(variant => variant.IsDefault)
                    .ThenBy(variant => variant.Id).Take(1))
                .ThenInclude(variant => variant.GalleryImages)
                .Include(product => product.Variants.OrderByDescending(variant => variant.IsDefault)
                    .ThenBy(variant => variant.Id).Take(1))
                .ThenInclude(variant => variant.Sizes)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

            var cards = products.Select(ToCard).ToList();
            lock (StockUpdateLock)
            {
                // Запрос мог прочитать остатки непосредственно перед подтверждением заказа.
                if (version != stockVersion) continue;
                foreach (var card in cards) cache.Set((typeof(ProductCardViewModel), card.Id), card);
                if (publishList) cache.Set(PublishedKey, cards);
                return cards;
            }
        }
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
            VariantId = variant?.Id ?? 0,
            Sizes = variant?.Sizes.Where(size => size.StockQuantity > 0)
                .OrderBy(size => size.Size)
                .Select(size => new ProductSizeViewModel { Id = size.Id, Size = size.Size, StockQuantity = size.StockQuantity })
                .ToArray() ?? [],
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
