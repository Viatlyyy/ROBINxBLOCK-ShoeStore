using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

public class ProductVariant
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    [Required, StringLength(80)] public string Name { get; set; } = "";
    [Required, StringLength(64)] public string Sku { get; set; } = "";
    [Required, StringLength(24)] public string Swatch { get; set; } = "#ffffff";
    [Required, StringLength(512)] public string ImageUrl { get; set; } = "";
    [StringLength(512)] public string? ThumbnailUrl { get; set; }
    public bool IsDefault { get; set; }
    public List<ProductVariantSize> Sizes { get; set; } = [];
    public List<ProductVariantImage> GalleryImages { get; set; } = [];
}
