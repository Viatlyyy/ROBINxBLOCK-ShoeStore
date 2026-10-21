namespace ShoeStore.Models;

public sealed record CatalogSelection(string Tag, string Sort)
{
    public static CatalogSelection FromQuery(string? tag, string? sort)
    {
        var selectedTag = tag?.Trim().ToLowerInvariant();
        var selectedSort = sort?.Trim().ToLowerInvariant();
        return new CatalogSelection(
            selectedTag is "new" or "popular" ? selectedTag : "all",
            selectedSort is "price-asc" or "price-desc" ? selectedSort : "default");
    }
}
