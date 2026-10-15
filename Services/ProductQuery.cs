using ShoeStore.Models;

namespace ShoeStore.Services;

public static class ProductQuery
{
    public static IQueryable<ProductCardViewModel> ApplySelection(IQueryable<ProductCardViewModel> products, CatalogSelection selection)
    {
        var query = selection.Tag switch
        {
            "new" => products.Where(product => product.IsNew),
            "popular" => products.Where(product => product.IsPopular),
            _ => products
        };
        return selection.Sort switch
        {
            "price-asc" => query.OrderBy(product => product.Price).ThenBy(product => product.Id),
            "price-desc" => query.OrderByDescending(product => product.Price).ThenBy(product => product.Id),
            _ => query.OrderByDescending(product => product.IsPopular)
                .ThenByDescending(product => product.IsNew).ThenBy(product => product.Id)
        };
    }
}
