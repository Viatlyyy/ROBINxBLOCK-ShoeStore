using System.ComponentModel.DataAnnotations;
using ShoeStore.Models;

namespace ShoeStore.Tests.Unit;

[TestClass]
public class TProductVariant
{
    [TestMethod]
    [DataRow(nameof(ProductVariant.Name), 0)]
    [DataRow(nameof(ProductVariant.Name), 81)]
    [DataRow(nameof(ProductVariant.Sku), 0)]
    [DataRow(nameof(ProductVariant.Sku), 65)]
    [DataRow(nameof(ProductVariant.Swatch), 0)]
    [DataRow(nameof(ProductVariant.Swatch), 25)]
    [DataRow(nameof(ProductVariant.ImageUrl), 0)]
    [DataRow(nameof(ProductVariant.ImageUrl), 513)]
    public void MissingOrOverlongField_IsRejected(string member, int length)
    {
        var variant = ValidVariant();
        var value = new string('x', length);
        switch (member)
        {
            case nameof(ProductVariant.Name): variant.Name = value; break;
            case nameof(ProductVariant.Sku): variant.Sku = value; break;
            case nameof(ProductVariant.Swatch): variant.Swatch = value; break;
            case nameof(ProductVariant.ImageUrl): variant.ImageUrl = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(member));
        }
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(variant, new ValidationContext(variant), errors, true);

        Assert.IsFalse(valid);
        Assert.IsTrue(errors.Any(error => error.MemberNames.Contains(member)));
    }

    [TestMethod]
    public void ValidVariant_PassesValidation()
    {
        var variant = ValidVariant();
        var errors = new List<ValidationResult>();

        Assert.IsTrue(Validator.TryValidateObject(variant, new ValidationContext(variant), errors, true));
        Assert.IsEmpty(errors);
    }

    private static ProductVariant ValidVariant() => new()
    {
        Id = 1, ProductId = 1, Name = "Sea Salt", Sku = "NB-9060-SEA-SALT",
        Swatch = "#e8e8e8", ImageUrl = "/images/products/9060.jpg", IsDefault = true
    };
}
