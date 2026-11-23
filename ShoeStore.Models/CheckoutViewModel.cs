using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

public class CheckoutViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Введите имя"), StringLength(80)] public string CustomerName { get; set; } = "";
    [StringLength(80)] public string? LastName { get; set; }
    [EmailAddress(ErrorMessage = "Укажите корректный email"), StringLength(254)] public string? Email { get; set; }
    [Required(ErrorMessage = "Введите телефон"), StringLength(32)] public string Phone { get; set; } = "";
    [Required, StringLength(32)] public string DeliveryMethod { get; set; } = "Курьер";
    [StringLength(80)] public string? City { get; set; }
    [StringLength(120)] public string? Street { get; set; }
    [StringLength(20)] public string? House { get; set; }
    [StringLength(20)] public string? Apartment { get; set; }
    [StringLength(12)] public string? Postcode { get; set; }
    [StringLength(500)] public string? Comment { get; set; }
    [Required, StringLength(24)] public string PaymentMethod { get; set; } = "card";
    public List<CartLine> Lines { get; set; } = [];
    public decimal Total => Lines.Sum(x => x.Total);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DeliveryMethod == "Курьер")
        {
            if (string.IsNullOrWhiteSpace(City)) yield return new ValidationResult("Выберите город.", [nameof(City)]);
            if (string.IsNullOrWhiteSpace(Street)) yield return new ValidationResult("Введите улицу.", [nameof(Street)]);
            if (string.IsNullOrWhiteSpace(House)) yield return new ValidationResult("Введите номер дома.", [nameof(House)]);
        }
        else if (DeliveryMethod == "Пункт выдачи" && string.IsNullOrWhiteSpace(City))
        {
            yield return new ValidationResult("Выберите город.", [nameof(City)]);
        }
    }
}
