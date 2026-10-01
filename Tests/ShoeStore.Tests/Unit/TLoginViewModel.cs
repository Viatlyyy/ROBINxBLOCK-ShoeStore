using System.ComponentModel.DataAnnotations;
using ShoeStore.Models;

namespace ShoeStore.Tests.Unit;


[TestClass]
public class TLoginViewModel
{
    [TestMethod]
    public void ValidData_PassesValidation()
    {
        // Корректные данные нужны как контрольный пример: обычная форма входа должна проходить проверку.
        var model = new LoginViewModel { Email = "user@example.com", Password = "Password1!" };
        var errors = new List<ValidationResult>();

        // В тесте MVC не участвует, поэтому проверяем модель.
        // Последний true включает все атрибуты свойств, в том числе EmailAddress и StringLength.
        var result = Validator.TryValidateObject(model, new ValidationContext(model), errors, true);

        // Проверяем и успех, и отсутствие ошибок — оба результата Validator должны согласоваться.
        Assert.IsTrue(result);
        Assert.IsEmpty(errors);
    }

    [TestMethod]
    // Каждая DataRow запускает метод отдельно: пустой email и разные ошибки в его формате.
    [DataRow("", "Введите адрес электронной почты.")]
    [DataRow("userexample.com", "Укажите корректный адрес электронной почты.")]
    [DataRow("@example.com", "Укажите корректный адрес электронной почты.")]
    [DataRow("user@", "Укажите корректный адрес электронной почты.")]
    [DataRow("user@@example.com", "Укажите корректный адрес электронной почты.")]
    public void InvalidEmail_ReturnsError(string email, string message)
    {
        // Пароль оставляем правильным, чтобы ошибка могла относиться только к email.
        var model = new LoginViewModel { Email = email, Password = "Password1!" };
        var errors = new List<ValidationResult>();

        // Проверяем поля модели вручную, потому что в тесте форма не отправляется.
        // Последний true включает все атрибуты свойств, в том числе EmailAddress и StringLength.
        var result = Validator.TryValidateObject(model, new ValidationContext(model), errors, true);

        Assert.IsFalse(result);
        // Одного false мало: проверяем поле Email и сообщение, которое увидит пользователь.
        Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(model.Email)) && error.ErrorMessage == message));
    }

    [TestMethod]
    // Пустое значение проверяет Required; семь символов — минимальную длину пароля.
    [DataRow("", "Введите пароль.")]
    [DataRow("1234567", "Пароль должен содержать от 8 до 128 символов.")]
    public void InvalidPassword_ReturnsError(string password, string message)
    {
        // Меняем только пароль. Некорректный email мог бы скрыть ошибку в проверяемом правиле.
        var model = new LoginViewModel { Email = "user@example.com", Password = password };
        var errors = new List<ValidationResult>();

        // Проверяем поля модели вручную, потому что в тесте форма не отправляется.
        // Последний true включает все атрибуты свойств, в том числе EmailAddress и StringLength.
        var result = Validator.TryValidateObject(model, new ValidationContext(model), errors, true);

        Assert.IsFalse(result);
        // Убеждаемся, что форма объяснит именно проблему с паролем.
        Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(model.Password)) && error.ErrorMessage == message));
    }
}
