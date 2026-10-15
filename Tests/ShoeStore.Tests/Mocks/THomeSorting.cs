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
        const string tag = "new";
        const string sort = "price-asc";
        var catalog = new Mock<ICatalogQueryService>();
        catalog.Setup(service => service.GetHomeAsync(It.IsAny<CatalogSelection>(), It.IsAny<CancellationToken>()))
            .Returns((CatalogSelection selection, CancellationToken token) =>
            {
                return Task.FromResult(new HomePageViewModel
                {
                    Selection = selection,
                    Products = [new ProductCardViewModel { Id = 7, Name = "9060", BrandName = "New Balance", Price = 22990m, ImageUrl = "/images/products/9060.jpg" }]
                });
            });

        var result = await new HomeController(catalog.Object).Sort(tag, sort);
        Assert.AreEqual("Index", ((ViewResult)result).ViewName);
        Assert.AreEqual(true, ((ViewResult)result).ViewData["IsHomePage"]);
        var model = (HomePageViewModel)((ViewResult)result).Model!;

        Assert.AreEqual(tag, model.Selection.Tag);
        Assert.AreEqual(sort, model.Selection.Sort);
        var card = model.Products.Single();
        Assert.AreEqual("9060", card.Name);
        Assert.AreEqual("New Balance", card.BrandName);
        Assert.AreEqual(22990m, card.Price);
        Assert.AreEqual("/images/products/9060.jpg", card.ImageUrl);
    }
}
