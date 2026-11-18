using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Unit;

[TestClass]
[TestCategory("CatalogSearch")]
public class TCatalogSearch
{
    [TestMethod]
    [DataRow(" dUnK ", 1)]
    [DataRow("bALANCE", 2)]
    [DataRow("   ", 0)]
    public void ApplySelection_SearchesNameAndBrand(string search, int matchId)
    {
        var cards = new[]
        {
            new ProductCardViewModel { Id = 1, Name = "Dunk Low", BrandName = "Nike" },
            new ProductCardViewModel { Id = 2, Name = "9060", BrandName = "New Balance" }
        };
        var selection = CatalogSelection.FromQuery(null, null, search);

        var result = ProductQuery.ApplySelection(cards, selection).Select(card => card.Id).ToArray();

        CollectionAssert.AreEqual(matchId == 0 ? new[] { 1, 2 } : new[] { matchId }, result);
    }

    [TestMethod]
    public void ApplySelection_FiltersByAnySelectedBrand_AndKeepsAllWithoutSelection()
    {
        var cards = new[]
        {
            new ProductCardViewModel { Id = 1, BrandId = 10 },
            new ProductCardViewModel { Id = 2, BrandId = 20 },
            new ProductCardViewModel { Id = 3, BrandId = 30 }
        };

        var selected = CatalogSelection.FromQuery(null, null, brandIds: [10, 20]);
        var allBrands = CatalogSelection.FromQuery(null, null, brandIds: []);

        var filteredIds = ProductQuery.ApplySelection(cards, selected).Select(card => card.Id).ToArray();
        var allIds = ProductQuery.ApplySelection(cards, allBrands).Select(card => card.Id).ToArray();

        CollectionAssert.AreEqual(new[] { 1, 2 }, filteredIds);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, allIds);
    }

    [TestMethod]
    [DataRow("price-asc", 2, 3, 1)]
    [DataRow("price-desc", 1, 2, 3)]
    public void ApplySelection_CombinesSearchGroupAndBrands(string sort, int first, int second, int third)
    {
        var cards = new[]
        {
            new ProductCardViewModel { Id = 1, BrandId = 10, Name = "Runner 1", IsNew = true, Price = 60m },
            new ProductCardViewModel { Id = 3, BrandId = 20, Name = "Runner 3", IsNew = true, Price = 20m },
            new ProductCardViewModel { Id = 2, BrandId = 10, Name = "Runner 2", IsNew = true, Price = 20m },
            new ProductCardViewModel { Id = 4, BrandId = 30, Name = "Runner 4", IsNew = true, Price = 1m },
            new ProductCardViewModel { Id = 5, BrandId = 10, Name = "Runner 5", IsNew = false, Price = 1m },
            new ProductCardViewModel { Id = 6, BrandId = 20, Name = "Campus", IsNew = true, Price = 1m }
        };
        var selection = CatalogSelection.FromQuery("new", sort, "runner", [10, 20]);

        var result = ProductQuery.ApplySelection(cards, selection).Select(card => card.Id).ToArray();

        CollectionAssert.AreEqual(new[] { first, second, third }, result);
        CollectionAssert.AreEqual(new[] { 1, 3, 2, 4, 5, 6 }, cards.Select(card => card.Id).ToArray());
    }
}
