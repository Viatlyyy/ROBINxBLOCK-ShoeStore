using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Models;


public class LoginViewModel
{
    [Required(ErrorMessage = "Введите адрес электронной почты."),
     EmailAddress(ErrorMessage = "Укажите корректный адрес электронной почты."),
     StringLength(254, ErrorMessage = "Адрес электронной почты не должен быть длиннее 254 символов.")]
    public string Email { get; set; } = "";


    [Required(ErrorMessage = "Введите пароль."),
     DataType(DataType.Password),
     StringLength(128, MinimumLength = 8, ErrorMessage = "Пароль должен содержать от 8 до 128 символов.")]
    public string Password { get; set; } = "";
}
