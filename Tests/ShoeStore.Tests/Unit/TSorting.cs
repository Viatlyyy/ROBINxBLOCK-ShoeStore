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
        var cards = new[]
        {
            new ProductCardViewModel { Id = 1, IsNew = true },
            new ProductCardViewModel { Id = 2, IsNew = false }
        }.AsQueryable();

        var result = ProductQuery.ApplySelection(cards, new CatalogSelection("new", "default"));

        CollectionAssert.AreEqual(new[] { 1 }, result.Select(product => product.Id).ToArray());
    }

    [TestMethod]
    [TestCategory("Sorting")]
    public void ApplySelection_Popular_ReturnsOnlyPopularCards()
    {
        var cards = new[]
        {
            new ProductCardViewModel { Id = 1, IsPopular = false },
            new ProductCardViewModel { Id = 2, IsPopular = true }
        }.AsQueryable();

        var result = ProductQuery.ApplySelection(cards, new CatalogSelection("popular", "default"));

        CollectionAssert.AreEqual(new[] { 2 }, result.Select(product => product.Id).ToArray());
    }

    [TestMethod]
    [TestCategory("Sorting")]
    public void ApplySelection_PriceAscending_OrdersFromCheapestToMostExpensive()
    {
        var cards = new[]
        {
            new ProductCardViewModel { Id = 2, Price = 50m },
            new ProductCardViewModel { Id = 1, Price = 20m },
            new ProductCardViewModel { Id = 3, Price = 70m }
        }.AsQueryable();

        var result = ProductQuery.ApplySelection(cards, new CatalogSelection("all", "price-asc"));

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, result.Select(product => product.Id).ToArray());
    }

    [TestMethod]
    [TestCategory("Sorting")]
    public void ApplySelection_PriceDescending_OrdersFromMostExpensiveToCheapest()
    {
        var cards = new[]
        {
            new ProductCardViewModel { Id = 2, Price = 50m },
            new ProductCardViewModel { Id = 1, Price = 20m },
            new ProductCardViewModel { Id = 3, Price = 70m }
        }.AsQueryable();

        var result = ProductQuery.ApplySelection(cards, new CatalogSelection("all", "price-desc"));

        CollectionAssert.AreEqual(new[] { 3, 2, 1 }, result.Select(product => product.Id).ToArray());
    }
}
