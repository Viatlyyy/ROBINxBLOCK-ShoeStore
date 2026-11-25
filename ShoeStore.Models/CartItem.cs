namespace ShoeStore.Models;

public sealed class CartItem
{
    public int ProductVariantId { get; init; }
    public int Size { get; init; }
    public int Quantity { get; set; }
}
