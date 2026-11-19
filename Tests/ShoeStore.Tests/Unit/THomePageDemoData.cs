using ShoeStore.Data;

namespace ShoeStore.Tests.Unit;

[TestClass]
public class THomePageDemoData
{
    [TestMethod]
    public void Create_ProvidesProductsForBothSectionsAndThreeHeroSlides()
    {
        var model = HomePageDemoData.Create();

        Assert.HasCount(16, model.Products);
        CollectionAssert.AreEquivalent(
            new[] { "9060", "3XL", "Dunk Low" },
            model.Products.Where(product => product.Name is "9060" or "3XL" or "Dunk Low")
                .Select(product => product.Name).ToArray());
        Assert.AreEqual(model.Products.Count, model.Products.Select(product => product.Id).Distinct().Count());
    }

    [TestMethod]
    public void Create_FillsRequiredProductDisplayFields()
    {
        var model = HomePageDemoData.Create();

        foreach (var product in model.Products)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(product.Name), $"Не задано название товара {product.Id}.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(product.BrandName), $"Не задан бренд товара {product.Id}.");
            Assert.IsGreaterThan(0m, product.Price, $"Некорректная цена товара {product.Id}.");
            Assert.AreEqual(1, product.Variants.Count(variant => variant.IsDefault));
            foreach (var variant in product.Variants)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(variant.Name));
                Assert.IsTrue(variant.ImageUrl.StartsWith("/images/", StringComparison.Ordinal));
                Assert.IsFalse(string.IsNullOrWhiteSpace(variant.Swatch));
            }
        }
    }

    [TestMethod]
    public void Create_ProvidesNonEmptyBrandsWithoutDuplicates()
    {
        var model = HomePageDemoData.Create();

        Assert.IsNotEmpty(model.Brands);
        Assert.IsTrue(model.Brands.All(brand => !string.IsNullOrWhiteSpace(brand)));
        CollectionAssert.AreEquivalent(model.Brands.ToArray(),
            model.Products.Select(product => product.BrandName).Distinct(StringComparer.Ordinal).ToArray());
    }
}
