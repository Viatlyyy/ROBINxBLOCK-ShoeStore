namespace ShoeStore.Models;

public sealed record CatalogSelection(string Tag, string Sort)
{
    public string SearchText { get; init; } = "";
    public IReadOnlyList<int> BrandIds { get; init; } = [];
    public bool HasFilters => Tag != "all" || SearchText.Length > 0 || BrandIds.Count > 0;

    public static CatalogSelection FromQuery(
        string? tag, string? sort, string? search = null, IEnumerable<int>? brandIds = null)
    {
        var selectedTag = tag?.Trim().ToLowerInvariant();
        var selectedSort = sort?.Trim().ToLowerInvariant();
        return new CatalogSelection(
            selectedTag is "new" or "popular" ? selectedTag : "all",
            selectedSort is "price-asc" or "price-desc" ? selectedSort : "default")
        {
            SearchText = search?.Trim() ?? "",
            BrandIds = brandIds?.Where(id => id > 0).Distinct().ToArray() ?? []
        };
    }
}
