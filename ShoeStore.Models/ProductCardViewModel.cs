namespace ShoeStore.Models;

public sealed class ProductCardViewModel
{
    public int Id { get; init; }
    public int BrandId { get; init; }
    public string Name { get; init; } = "";
    public string BrandName { get; init; } = "";
    public decimal Price { get; init; }
    public string ImageUrl { get; init; } = "";
    public bool IsNew { get; init; }
    public bool IsPopular { get; init; }
    public List<ProductCardVariantViewModel> Variants { get; init; } = [];
}
