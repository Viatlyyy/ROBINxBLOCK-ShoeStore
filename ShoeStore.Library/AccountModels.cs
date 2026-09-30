using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ShoeStore.Models;

// Дополнительных полей пока нет. Имя покупателя сохраняем в claim через AccountController.
public class ApplicationUser : IdentityUser
{
}

// Данные формы, а не запись из базы. Атрибуты задают правила проверки в MVC и в тестах.
public class LoginViewModel
{
    [Required(ErrorMessage = "Введите адрес электронной почты."),
     EmailAddress(ErrorMessage = "Укажите корректный адрес электронной почты."),
     StringLength(254, ErrorMessage = "Адрес электронной почты не должен быть длиннее 254 символов.")]
    public string Email { get; set; } = "";

    // DataType помогает Razor выбрать скрытый ввод. Состав пароля при регистрации проверяет Identity.
    [Required(ErrorMessage = "Введите пароль."),
     DataType(DataType.Password),
     StringLength(128, MinimumLength = 8, ErrorMessage = "Пароль должен содержать от 8 до 128 символов.")]
    public string Password { get; set; } = "";
}

// Email и пароль имеют те же правила, что и при входе, поэтому не дублируем их.
public class RegisterViewModel : LoginViewModel
{
    [Required(ErrorMessage = "Введите имя."),
     StringLength(50, ErrorMessage = "Имя не должно быть длиннее 50 символов.")]
    public string FirstName { get; set; } = "";

    // Compare сверяет поля формы. Подтверждение пароля в базу не сохраняется.
    [Required(ErrorMessage = "Повторите пароль."),
     DataType(DataType.Password),
     Compare(nameof(Password), ErrorMessage = "Пароли не совпадают."),
     StringLength(128, ErrorMessage = "Пароль не должен быть длиннее 128 символов.")]
    public string ConfirmPassword { get; set; } = "";

    // Required для bool пропустил бы false. Range разрешает только true — галочка должна быть отмечена.
    [Range(typeof(bool), "true", "true", ErrorMessage = "Необходимо принять условия использования и политику конфиденциальности.")]
    public bool AcceptTerms { get; set; }
}
