using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

public class ProductVariantSize { public int Id { get; set; } public int ProductVariantId { get; set; } public ProductVariant ProductVariant { get; set; } = null!; [Range(30, 50)] public int Size { get; set; } [Range(0, 999)] public int StockQuantity { get; set; } }
