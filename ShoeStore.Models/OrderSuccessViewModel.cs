namespace ShoeStore.Models;

public sealed class OrderSuccessViewModel
{
    public int OrderNumber { get; init; }
    public Guid PublicId { get; init; }
    public bool CanViewOrder { get; init; }
}
