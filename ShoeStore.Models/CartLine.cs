namespace ShoeStore.Models;

public sealed class CartLine
{
    public required ProductVariant Variant { get; init; }
    public int Size { get; init; }
    public int Quantity { get; set; }
    public int MaximumQuantity { get; set; }
    public Product Product => Variant.Product;
    public decimal Total => Product.Price * Quantity;
}
