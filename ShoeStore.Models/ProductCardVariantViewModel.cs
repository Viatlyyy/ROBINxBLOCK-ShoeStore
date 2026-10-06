namespace ShoeStore.Models;

public sealed class ProductCardVariantViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Swatch { get; init; } = "#ffffff";
    public string ImageUrl { get; init; } = "";
    public string? ThumbnailUrl { get; init; }
    public bool IsDefault { get; init; }
}
