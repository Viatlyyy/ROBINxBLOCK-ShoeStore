namespace ShoeStore.Models;

public sealed class HomePageViewModel
{
    public List<ProductCardViewModel> Products { get; init; } = [];
    public List<string> Brands { get; init; } = [];
    public CatalogSelection Selection { get; init; } = new("all", "default");
}
