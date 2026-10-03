using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;

// Данные формы входа. Эта модель не является базовым классом формы регистрации.
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
