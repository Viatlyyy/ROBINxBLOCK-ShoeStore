using Microsoft.AspNetCore.Mvc;
using Moq;
using ShoeStore.Controllers;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Mocks;

[TestClass]
public class THomeSorting
{
    [TestMethod]
    [TestCategory("CatalogSearch")]
    public async Task SearchResults_PassesSelectionAndReturnsPartialView()
    {
        var expected = new HomePageViewModel
        {
            Products = [new ProductCardViewModel { Id = 7, Name = "Dunk Low", BrandId = 10 }]
        };
        var catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        catalog.Setup(service => service.GetHomeAsync(
                It.Is<CatalogSelection>(selection =>
                    selection.Tag == "new" && selection.Sort == "price-desc"
                    && selection.SearchText == "Dunk" && selection.BrandIds.SequenceEqual(new[] { 10, 20 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = (PartialViewResult)await new HomeController(catalog.Object)
            .SearchResults("new", "price-desc", " Dunk ", [10, 20, 10]);

        Assert.AreEqual("_CatalogProducts", result.ViewName);
        Assert.AreSame(expected, result.Model);
    }

    [TestMethod]
    [TestCategory("Sorting")]
    public async Task Sort_PreservesSelectionAndCardData()
    {
        var expected = new HomePageViewModel
        {
            Selection = new CatalogSelection("new", "price-asc"),
            Products = [new ProductCardViewModel { Id = 7, Name = "9060", Price = 22990m }]
        };
        var catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        catalog.Setup(service => service.GetHomeAsync(
                It.Is<CatalogSelection>(selection => selection.Tag == "new" && selection.Sort == "price-asc"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = (ViewResult)await new HomeController(catalog.Object).Sort("new", "price-asc");

        Assert.AreEqual("Index", result.ViewName);
        Assert.AreSame(expected, result.Model);
    }
}
