using System.Globalization;
using ShoeStore.Models;

namespace ShoeStore.Tests.Unit;

[TestClass]
public class TProduct
{
    private static Product ValidProduct() => new()
    {
        Name = "New Balance 9060",
        Description = "Кроссовки для повседневной носки.",
        Price = 14990.50m
    };

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public void MissingName_FailsValidation(string? name)
    {
        var product = ValidProduct();
        product.Name = name!;

        ModelValidation.AssertInvalid(product, nameof(Product.Name));
    }

    [TestMethod]
    public void NameLongerThan100_FailsValidation()
    {
        var product = ValidProduct();
        product.Name = new string('N', 101);

        ModelValidation.AssertInvalid(product, nameof(Product.Name));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public void MissingDescription_FailsValidation(string? description)
    {
        var product = ValidProduct();
        product.Description = description!;

        ModelValidation.AssertInvalid(product, nameof(Product.Description));
    }

    [TestMethod]
    public void DescriptionLongerThan2000_FailsValidation()
    {
        var product = ValidProduct();
        product.Description = new string('D', 2001);

        ModelValidation.AssertInvalid(product, nameof(Product.Description));
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("0")]
    [DataRow("0.99")]
    [DataRow("999999.01")]
    [DataRow("1000000")]
    public void PriceOutsideAllowedRange_FailsValidation(string price)
    {
        var product = ValidProduct();
        // Строка сохраняет точность decimal и не зависит от региональных настроек компьютера.
        product.Price = decimal.Parse(price, CultureInfo.InvariantCulture);

        ModelValidation.AssertInvalid(product, nameof(Product.Price));
    }

    [TestMethod]
    public void ValidData_PassesValidation() => ModelValidation.AssertValid(ValidProduct());

    [TestMethod]
    [DataRow("1")]
    [DataRow("999999")]
    public void MaximumTextLengthsAndBoundaryPrice_PassValidation(string price)
    {
        var product = ValidProduct();
        product.Name = new string('N', 100);
        product.Description = new string('D', 2000);
        product.Price = decimal.Parse(price, CultureInfo.InvariantCulture);

        ModelValidation.AssertValid(product);
    }
}
