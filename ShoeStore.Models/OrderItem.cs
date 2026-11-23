using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public int ProductId { get; set; }
    public int ProductVariantId { get; set; }
    [Required, StringLength(180)]
    public string ProductName { get; set; } = "";
    [Required, StringLength(80)]
    public string VariantName { get; set; } = "";
    [Required, StringLength(64)]
    public string Sku { get; set; } = "";
    [Required, StringLength(512)]
    public string ImageUrl { get; set; } = "";
    public int Size { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
