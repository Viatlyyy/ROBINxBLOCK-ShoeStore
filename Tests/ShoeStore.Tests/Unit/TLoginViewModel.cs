using System.ComponentModel.DataAnnotations;
using ShoeStore.Models;

namespace ShoeStore.Tests.Unit;


[TestClass]
public class TLoginViewModel
{
    [TestMethod]
    public void ValidData_PassesValidation()
    {

        var model = new LoginViewModel { Email = "user@example.com", Password = "Password1!" };
        var errors = new List<ValidationResult>();



        var result = Validator.TryValidateObject(model, new ValidationContext(model), errors, true);


        Assert.IsTrue(result);
        Assert.IsEmpty(errors);
    }

    [TestMethod]

    [DataRow("", "Введите адрес электронной почты.")]
    [DataRow("userexample.com", "Укажите корректный адрес электронной почты.")]
    [DataRow("@example.com", "Укажите корректный адрес электронной почты.")]
    [DataRow("user@", "Укажите корректный адрес электронной почты.")]
    [DataRow("user@@example.com", "Укажите корректный адрес электронной почты.")]
    public void InvalidEmail_ReturnsError(string email, string message)
    {

        var model = new LoginViewModel { Email = email, Password = "Password1!" };
        var errors = new List<ValidationResult>();



        var result = Validator.TryValidateObject(model, new ValidationContext(model), errors, true);

        Assert.IsFalse(result);

        Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(model.Email)) && error.ErrorMessage == message));
    }

    [TestMethod]

    [DataRow("", "Введите пароль.")]
    [DataRow("1234567", "Пароль должен содержать от 8 до 128 символов.")]
    public void InvalidPassword_ReturnsError(string password, string message)
    {

        var model = new LoginViewModel { Email = "user@example.com", Password = password };
        var errors = new List<ValidationResult>();



        var result = Validator.TryValidateObject(model, new ValidationContext(model), errors, true);

        Assert.IsFalse(result);

        Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(model.Password)) && error.ErrorMessage == message));
    }
}
