namespace ShoeStore.Models;

public sealed class HomePageViewModel
{
    public List<ProductCardViewModel> Products { get; init; } = [];
    public List<string> Brands { get; init; } = [];
    public IReadOnlyDictionary<int, string> CatalogBrands { get; init; } = new Dictionary<int, string>();
    public CatalogSelection Selection { get; init; } = new("all", "default");
}
