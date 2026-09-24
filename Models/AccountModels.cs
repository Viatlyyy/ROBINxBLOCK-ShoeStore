using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ShoeStore.Models;

public class ApplicationUser : IdentityUser
{
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Введите имя."),
     StringLength(50, ErrorMessage = "Имя не должно быть длиннее 50 символов.")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Введите адрес электронной почты."),
     EmailAddress(ErrorMessage = "Укажите корректный адрес электронной почты."),
     StringLength(254, ErrorMessage = "Адрес электронной почты не должен быть длиннее 254 символов.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Введите пароль."),
     DataType(DataType.Password),
     StringLength(128, MinimumLength = 8, ErrorMessage = "Пароль должен содержать от 8 до 128 символов.")]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Повторите пароль."),
     DataType(DataType.Password),
     Compare(nameof(Password), ErrorMessage = "Пароли не совпадают."),
     StringLength(128, ErrorMessage = "Пароль не должен быть длиннее 128 символов.")]
    public string ConfirmPassword { get; set; } = "";

    [Range(typeof(bool), "true", "true", ErrorMessage = "Необходимо принять условия использования и политику конфиденциальности.")]
    public bool AcceptTerms { get; set; }
}
