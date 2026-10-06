using System.ComponentModel.DataAnnotations;

namespace ShoeStore.Tests.Unit;

internal static class ModelValidation
{
    public static void AssertValid(object model)
    {
        var errors = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true);

        Assert.IsTrue(valid, string.Join("; ", errors.Select(error => error.ErrorMessage)));
        Assert.IsEmpty(errors);
    }

    public static void AssertInvalid(object model, string member)
    {
        var errors = new List<ValidationResult>();
        var valid = Validator.TryValidateObject(model, new ValidationContext(model), errors, validateAllProperties: true);

        Assert.IsFalse(valid, $"Некорректное поле {member} прошло валидацию.");
        // Остальные поля корректны: ошибка должна относиться только к проверяемому свойству.
        Assert.AreEqual(1, errors.Count);
        CollectionAssert.AreEqual(new[] { member }, errors[0].MemberNames.ToArray());
    }
}
