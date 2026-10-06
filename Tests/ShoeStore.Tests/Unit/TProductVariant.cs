using ShoeStore.Models;

namespace ShoeStore.Tests.Unit;

[TestClass]
public class TProductVariant
{
    private static ProductVariant ValidVariant() => new()
    {
        Name = "Чёрный / серый",
        Sku = "NB-9060-BLACK",
        Swatch = "#242424",
        ImageUrl = "/images/products/9060-black-grey.png"
    };

    [TestMethod]
    public void MissingName_FailsValidation()
    {
        var variant = ValidVariant();
        variant.Name = "";

        ModelValidation.AssertInvalid(variant, nameof(ProductVariant.Name));
    }

    [TestMethod]
    public void NameLongerThan80_FailsValidation()
    {
        var variant = ValidVariant();
        variant.Name = new string('N', 81);

        ModelValidation.AssertInvalid(variant, nameof(ProductVariant.Name));
    }

    [TestMethod]
    public void MissingSku_FailsValidation()
    {
        var variant = ValidVariant();
        variant.Sku = "";

        ModelValidation.AssertInvalid(variant, nameof(ProductVariant.Sku));
    }

    [TestMethod]
    public void SkuLongerThan64_FailsValidation()
    {
        var variant = ValidVariant();
        variant.Sku = new string('S', 65);

        ModelValidation.AssertInvalid(variant, nameof(ProductVariant.Sku));
    }

    [TestMethod]
    public void MissingSwatch_FailsValidation()
    {
        var variant = ValidVariant();
        variant.Swatch = "";

        ModelValidation.AssertInvalid(variant, nameof(ProductVariant.Swatch));
    }

    [TestMethod]
    public void SwatchLongerThan24_FailsValidation()
    {
        var variant = ValidVariant();
        variant.Swatch = new string('C', 25);

        ModelValidation.AssertInvalid(variant, nameof(ProductVariant.Swatch));
    }

    [TestMethod]
    public void MissingImageUrl_FailsValidation()
    {
        var variant = ValidVariant();
        variant.ImageUrl = "";

        ModelValidation.AssertInvalid(variant, nameof(ProductVariant.ImageUrl));
    }

    [TestMethod]
    public void ImageUrlLongerThan512_FailsValidation()
    {
        var variant = ValidVariant();
        variant.ImageUrl = new string('I', 513);

        ModelValidation.AssertInvalid(variant, nameof(ProductVariant.ImageUrl));
    }

    [TestMethod]
    public void ValidData_PassesValidation() => ModelValidation.AssertValid(ValidVariant());
}
