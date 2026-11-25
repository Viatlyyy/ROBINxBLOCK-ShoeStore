namespace ShoeStore.Models;

public sealed record ProductCardVariantViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Swatch { get; init; } = "#ffffff";
    public string ImageUrl { get; init; } = "";
    public string? ThumbnailUrl { get; init; }
    public bool IsDefault { get; init; }
    public IReadOnlyList<ProductSizeViewModel> Sizes { get; init; } = [];
    public IReadOnlyList<ProductGalleryPhoto> Photos { get; init; } = [];
}

public sealed record ProductGalleryPhoto(string ImageUrl, string ThumbnailUrl);
