using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

public sealed class CustomerProfileViewModel
{
    [Required(ErrorMessage = "Введите имя."), StringLength(80, ErrorMessage = "Имя не должно быть длиннее 80 символов.")]
    public string DisplayName { get; set; } = "";

    [Phone(ErrorMessage = "Укажите телефон в корректном формате."), StringLength(32)]
    public string? Phone { get; set; }

    public string? Email { get; set; }
}

public sealed record AccountOrderSummary(Guid PublicId, int Number, DateTime CreatedAt, string Status, decimal Total, string? ImageUrl);

public sealed record AccountOverviewViewModel(CustomerProfileViewModel Profile, AccountOrderSummary? LatestOrder,
    IReadOnlyList<ProductCardViewModel> Favorites);

public sealed record AccountOrdersViewModel(IReadOnlyList<AccountOrderSummary> Orders, int Page, int TotalPages);

public sealed record AccountOrderLine(string Name, string VariantName, int Size, int Quantity,
    decimal Price, string ImageUrl);

public sealed record AccountOrderDetails(AccountOrderSummary Order, string DeliveryMethod, string? Address,
    string PaymentMethod, IReadOnlyList<AccountOrderLine> Lines);
