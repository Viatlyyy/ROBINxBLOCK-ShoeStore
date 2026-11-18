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
