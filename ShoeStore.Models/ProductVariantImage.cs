using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

public class ProductVariantImage { public int Id { get; set; } public int ProductVariantId { get; set; } public ProductVariant ProductVariant { get; set; } = null!; [Required, StringLength(512)] public string ImageUrl { get; set; } = ""; [StringLength(512)] public string? ThumbnailUrl { get; set; } [Range(0, 99)] public int SortOrder { get; set; } }
