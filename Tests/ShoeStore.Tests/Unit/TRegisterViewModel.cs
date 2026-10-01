using System.ComponentModel.DataAnnotations;
using ShoeStore.Models;

namespace ShoeStore.Tests.Unit;

// Проверяем модель формы из библиотеки ShoeStore.Models без сервера и базы данных.

[TestClass]
[TestCategory("Unit")]
public class TRegisterViewModel
{

    [TestMethod]
    public void ValidForm_HasNoErrors()
    {
        // создаём форму, в которой все поля заполнены правильно.
        var form = CreateValidForm();

        // запускаем правила проверки модели и получаем сообщения об ошибках.
        var errors = GetValidationErrors(form);

        // Assert сравнивает ожидаемое значение с фактическим; ошибок должно быть 0.
        Assert.AreEqual(0, errors.Count);
    }

    [TestMethod]
    public void EmptyFirstName_ReturnsError()
    {
        var form = CreateValidForm();
        // Меняем только имя, чтобы проверять именно его обязательность.
        form.FirstName = "";

        var errors = GetValidationErrors(form);

        // CollectionAssert.Contains проверяет наличие указанного сообщения в списке.
        CollectionAssert.Contains(errors, "Введите имя.");
    }

    [TestMethod]
    public void EmptyEmail_ReturnsError()
    {
        var form = CreateValidForm();
        // Пустой e-mail должен нарушить правило Required у свойства Email.
        form.Email = "";

        var errors = GetValidationErrors(form);

        CollectionAssert.Contains(errors, "Введите адрес электронной почты.");
    }

    [TestMethod]
    public void EmptyPassword_ReturnsError()
    {
        var form = CreateValidForm();
        // Остальные поля корректны; проверяем сообщение об отсутствии пароля.
        form.Password = "";

        var errors = GetValidationErrors(form);

        CollectionAssert.Contains(errors, "Введите пароль.");
    }

    [TestMethod]
    public void EmptyConfirmPassword_ReturnsError()
    {
        var form = CreateValidForm();
        // Даже при заполненном пароле его подтверждение обязательно.
        form.ConfirmPassword = "";

        var errors = GetValidationErrors(form);

        CollectionAssert.Contains(errors, "Повторите пароль.");
    }

    [TestMethod]
    public void TermsNotAccepted_ReturnsError()
    {
        var form = CreateValidForm();
        // false означает, что пользователь не поставил галочку согласия.
        form.AcceptTerms = false;

        var errors = GetValidationErrors(form);

        CollectionAssert.Contains(errors, "Необходимо принять условия использования и политику конфиденциальности.");
    }

    [TestMethod]
    // Каждый DataRow запускает этот метод отдельно и передаёт свой адрес в параметр email.
    [DataRow("user.example.com")]
    [DataRow("user@@example.com")]
    [DataRow("@example.com")]
    public void InvalidEmail_ReturnsError(string email)
    {
        var form = CreateValidForm();
        // Все три адреса нарушают формат, проверяемый атрибутом EmailAddress.
        form.Email = email;

        var errors = GetValidationErrors(form);

        CollectionAssert.Contains(errors, "Укажите корректный адрес электронной почты.");
    }

    [TestMethod]
    // Проверяем пароли длиной 4 и 7 символов: оба короче минимальных 8.
    [DataRow("A1!a")]
    [DataRow("A1!aaaa")]
    public void ShortPassword_ReturnsError(string password)
    {
        var form = CreateValidForm();
        form.Password = password;
        // Подтверждение делаем таким же, чтобы не добавлять ошибку несовпадения паролей.
        form.ConfirmPassword = password;

        var errors = GetValidationErrors(form);

        CollectionAssert.Contains(errors, "Пароль должен содержать от 8 до 128 символов.");
    }

    [TestMethod]
    public void DifferentPasswords_ReturnsError()
    {
        var form = CreateValidForm();
        // Пароль оставляем прежним, а подтверждение делаем другим: проверяется Compare.
        form.ConfirmPassword = "Another1!";

        var errors = GetValidationErrors(form);

        CollectionAssert.Contains(errors, "Пароли не совпадают.");
    }

    // Возвращаем новый объект для каждого теста, чтобы изменения не переходили между тестами.
    // => new() — сокращённая запись метода, который создаёт и возвращает RegisterViewModel.
    private static RegisterViewModel CreateValidForm() => new()
    {
        FirstName = "Виталий",
        Email = "customer@example.com",
        Password = "TestPass1!",
        ConfirmPassword = "TestPass1!",
        AcceptTerms = true
    };

    private static List<string> GetValidationErrors(RegisterViewModel form)
    {
        // В results будут записаны нарушения правил Required, StringLength, Compare и других.
        var results = new List<ValidationResult>();
        // ValidationContext описывает проверяемый объект; true включает проверку всех свойств.
        // Этот вызов проверяет атрибуты модели, но не сложность пароля на уровне Identity.
        Validator.TryValidateObject(form, new ValidationContext(form), results, validateAllProperties: true);
        // Select оставляет только тексты ошибок; ?? заменяет возможный null пустой строкой.
        return results.Select(result => result.ErrorMessage ?? "").ToList();
    }
}
