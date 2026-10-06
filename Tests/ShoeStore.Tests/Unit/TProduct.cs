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
    public void MissingName_FailsValidation()
    {
        var product = ValidProduct();
        product.Name = "";

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
    public void MissingDescription_FailsValidation()
    {
        var product = ValidProduct();
        product.Description = "";

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
    [DataRow("0.99")]
    [DataRow("999999.01")]
    public void PriceOutsideAllowedRange_FailsValidation(string price)
    {
        var product = ValidProduct();
        // Строка сохраняет точность decimal и не зависит от региональных настроек компьютера.
        product.Price = decimal.Parse(price, CultureInfo.InvariantCulture);

        ModelValidation.AssertInvalid(product, nameof(Product.Price));
    }

    [TestMethod]
    public void ValidData_PassesValidation() => ModelValidation.AssertValid(ValidProduct());
}
