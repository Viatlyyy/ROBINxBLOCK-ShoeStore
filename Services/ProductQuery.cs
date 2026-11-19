using ShoeStore.Models;

namespace ShoeStore.Services;

public static class ProductQuery
{
    public static IEnumerable<ProductCardViewModel> ApplySelection(
        IEnumerable<ProductCardViewModel> products, CatalogSelection selection)
    {
        var query = selection.Tag switch
        {
            "new" => products.Where(product => product.IsNew),
            "popular" => products.Where(product => product.IsPopular),
            _ => products
        };

        if (selection.SearchText.Length > 0)
            query = query.Where(product =>
                product.Name.Contains(selection.SearchText, StringComparison.OrdinalIgnoreCase)
                || product.BrandName.Contains(selection.SearchText, StringComparison.OrdinalIgnoreCase));

        if (selection.BrandIds.Count > 0)
            query = query.Where(product => selection.BrandIds.Contains(product.BrandId));

        return selection.Sort switch
        {
            "price-asc" => query.OrderBy(product => product.Price).ThenBy(product => product.Id),
            "price-desc" => query.OrderByDescending(product => product.Price).ThenBy(product => product.Id),
            _ => query.OrderByDescending(product => product.IsPopular)
                .ThenByDescending(product => product.IsNew).ThenBy(product => product.Id)
        };
    }
}
