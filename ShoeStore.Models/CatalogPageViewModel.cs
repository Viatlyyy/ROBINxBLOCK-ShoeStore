namespace ShoeStore.Models;

public sealed class CatalogPageViewModel
{
    public List<ProductCardViewModel> Products { get; init; } = [];
    public int Page { get; init; }
    public int TotalPages { get; init; }
}
