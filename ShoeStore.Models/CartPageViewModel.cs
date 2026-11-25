namespace ShoeStore.Models;

public sealed class CartPageViewModel
{
    public required List<CartLine> Lines { get; init; }

    public static string ItemsLabel(int count)
    {
        var word = (count % 100, count % 10) switch
        {
            (>= 11 and <= 14, _) => "товаров",
            (_, 1) => "товар",
            (_, >= 2 and <= 4) => "товара",
            _ => "товаров"
        };
        return $"{count} {word}";
    }
}
