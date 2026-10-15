using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Unit;

[TestClass]
public class TSorting
{
    [TestMethod]
    [TestCategory("Sorting")]
    public void ApplySelection_New_ReturnsOnlyNewCards()
    {
        var result = ProductQuery.ApplySelection(CreateCards(), new CatalogSelection("new", "default")).ToArray();

        CollectionAssert.AreEquivalent(new[] { 1, 3 }, result.Select(product => product.Id).ToArray());
        Assert.IsTrue(result.All(product => product.IsNew));
    }

    [TestMethod]
    [TestCategory("Sorting")]
    public void ApplySelection_Popular_ReturnsOnlyPopularCards()
    {
        var result = ProductQuery.ApplySelection(CreateCards(), new CatalogSelection("popular", "default")).ToArray();

        CollectionAssert.AreEquivalent(new[] { 2, 3 }, result.Select(product => product.Id).ToArray());
        Assert.IsTrue(result.All(product => product.IsPopular));
    }

    [TestMethod]
    [TestCategory("Sorting")]
    public void ApplySelection_PriceAscending_OrdersFromCheapestToMostExpensive()
    {
        var result = ProductQuery.ApplySelection(CreateCards(), new CatalogSelection("all", "price-asc"));

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, result.Select(product => product.Id).ToArray());
    }

    [TestMethod]
    [TestCategory("Sorting")]
    public void ApplySelection_PriceDescending_OrdersFromMostExpensiveToCheapest()
    {
        var result = ProductQuery.ApplySelection(CreateCards(), new CatalogSelection("all", "price-desc"));

        CollectionAssert.AreEqual(new[] { 3, 2, 1 }, result.Select(product => product.Id).ToArray());
    }

    private static IQueryable<ProductCardViewModel> CreateCards() => new[]
    {
        new ProductCardViewModel { Id = 2, Price = 50m, IsPopular = true },
        new ProductCardViewModel { Id = 1, Price = 20m, IsNew = true },
        new ProductCardViewModel { Id = 3, Price = 70m, IsNew = true, IsPopular = true }
    }.AsQueryable();
}
