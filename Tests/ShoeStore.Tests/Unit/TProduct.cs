using System.ComponentModel.DataAnnotations;
using System.Globalization;
using ShoeStore.Models;

namespace ShoeStore.Tests.Unit;

[TestClass]
public class TProduct
{
    [TestMethod]
    public void MissingName_IsRejected()
    {
        var product = ValidProduct();
        product.Name = "";
        AssertInvalid(product, nameof(Product.Name));
    }

    [TestMethod]
    public void NameLongerThan100_IsRejected()
    {
        var product = ValidProduct();
        product.Name = new string('x', 101);
        AssertInvalid(product, nameof(Product.Name));
    }

    [TestMethod]
    public void MissingDescription_IsRejected()
    {
        var product = ValidProduct();
        product.Description = "";
        AssertInvalid(product, nameof(Product.Description));
    }

    [TestMethod]
    public void DescriptionLongerThan2000_IsRejected()
    {
        var product = ValidProduct();
        product.Description = new string('x', 2001);
        AssertInvalid(product, nameof(Product.Description));
    }

    [TestMethod]
    [DataRow("0.99", false)]
    [DataRow("999999.01", false)]
    [DataRow("1", true)]
    [DataRow("999999", true)]
    public void Price_UsesExactDecimalLimits(string price, bool expectedValid)
    {
        var product = ValidProduct();
        product.Price = decimal.Parse(price, CultureInfo.InvariantCulture);
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(product, new ValidationContext(product), errors, true);

        Assert.AreEqual(expectedValid, valid);
        if (expectedValid) Assert.IsEmpty(errors);
        else Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(nameof(Product.Price))));
    }

    [TestMethod]
    public void ValidProduct_PassesValidation()
    {
        var product = ValidProduct();
        var errors = new List<ValidationResult>();

        Assert.IsTrue(Validator.TryValidateObject(product, new ValidationContext(product), errors, true));
        Assert.IsEmpty(errors);
    }

    private static Product ValidProduct() => new()
    {
        Id = 1, Name = "9060", Description = "Кроссовки для города.", Price = 22990m,
        BrandId = 1, CategoryId = 1, Status = ProductStatus.Active
    };

    private static void AssertInvalid(Product product, string member)
    {
        var errors = new List<ValidationResult>();
        Assert.IsFalse(Validator.TryValidateObject(product, new ValidationContext(product), errors, true));
        Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(member)));
    }
}
