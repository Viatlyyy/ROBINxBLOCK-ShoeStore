namespace ShoeStore.Models;

public sealed record ProductSizeViewModel
{
    public int Id { get; init; }
    public int Size { get; init; }
    public int StockQuantity { get; init; }
}
