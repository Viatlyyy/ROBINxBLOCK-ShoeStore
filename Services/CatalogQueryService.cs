using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Services;

public sealed class CatalogQueryService(ApplicationDbContext db, IMemoryCache cache) : ICatalogQueryService
{
    public const int CatalogPageSize = 24;
    private const string PublishedCardIdsKey = "Catalog.PublishedCardIds";
    private static readonly SemaphoreSlim ReloadCatalog = new(1, 1);

    public async Task<HomePageViewModel> GetHomeAsync(CatalogSelection selection, CancellationToken cancellationToken)
    {
        var cards = await GetPublishedCardsAsync(cancellationToken);
        return new HomePageViewModel
        {
            Products = ProductQuery.ApplySelection(cards, selection).ToList(),
            Brands = HomePageDemoData.Create().Brands,
            Selection = selection
        };
    }

    public async Task<HomePageViewModel> GetHomeAsync(CancellationToken cancellationToken)
    {
        var products = await CardQuery()
            .OrderByDescending(product => product.IsPopular)
            .ThenByDescending(product => product.IsNew)
            .ThenBy(product => product.Id)
            .ToListAsync(cancellationToken);

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

    private async Task<List<ProductCardViewModel>> GetPublishedCardsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cards = ReadCachedCards();
        if (cards is not null)
            return cards;

        await ReloadCatalog.WaitAsync(cancellationToken);
        try
        {
            cards = ReadCachedCards();
            if (cards is not null)
                return cards;

            cards = await CardQuery().ToListAsync(cancellationToken);
            foreach (var card in cards)
                cache.Set((typeof(ProductCardViewModel), card.Id), card);

            cache.Set(PublishedCardIdsKey, cards.Select(card => card.Id).ToArray());
            return cards;
        }
        finally
        {
            ReloadCatalog.Release();
        }
    }

    private List<ProductCardViewModel>? ReadCachedCards()
    {
        var ids = cache.Get<int[]>(PublishedCardIdsKey);
        if (ids is null)
            return null;

        var cards = new List<ProductCardViewModel>(ids.Length);
        foreach (var id in ids)
        {
            var card = GetProduct(id);
            if (card is null)
                return null;

            cards.Add(card);
        }

        return cards;
    }

    private ProductCardViewModel? GetProduct(int id) =>
        cache.Get<ProductCardViewModel>((typeof(ProductCardViewModel), id));

    private IQueryable<ProductCardViewModel> CardQuery() => ToCards(
        db.Products.AsNoTracking().Where(product => product.Status == ProductStatus.Active));

    private static IQueryable<ProductCardViewModel> ToCards(IQueryable<Product> query) => query.Select(product => new ProductCardViewModel
    {
        Id = product.Id,
        BrandId = product.BrandId,
        Name = product.Name,
        BrandName = product.Brand.Name,
        Price = product.Price,
        ImageUrl = product.ImageUrl ?? "",
        IsNew = product.IsNew,
        IsPopular = product.IsPopular,
    });

}
