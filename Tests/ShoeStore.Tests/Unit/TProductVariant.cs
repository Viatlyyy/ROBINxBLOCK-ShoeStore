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
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public void MissingName_FailsValidation(string? name)
    {
        var variant = ValidVariant();
        variant.Name = name!;

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
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public void MissingSku_FailsValidation(string? sku)
    {
        var variant = ValidVariant();
        variant.Sku = sku!;

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
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public void MissingSwatch_FailsValidation(string? swatch)
    {
        var variant = ValidVariant();
        variant.Swatch = swatch!;

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
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public void MissingImageUrl_FailsValidation(string? imageUrl)
    {
        var variant = ValidVariant();
        variant.ImageUrl = imageUrl!;

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

    [TestMethod]
    public void MaximumFieldLengths_PassValidation()
    {
        var variant = ValidVariant();
        variant.Name = new string('N', 80);
        variant.Sku = new string('S', 64);
        variant.Swatch = new string('C', 24);
        variant.ImageUrl = new string('I', 512);

        ModelValidation.AssertValid(variant);
    }
}
