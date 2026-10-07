using Microsoft.AspNetCore.Mvc;
using Moq;
using ShoeStore.Controllers;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Mocks;

[TestClass]
public class TCatalogController
{
    [TestMethod]
    public async Task New_ReturnsOnlyNewModelsAndPassesToken()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var service = QueryService(MixedProducts(), token);
        var controller = new CatalogController(service.Object);

        var result = await controller.New(token);

        var products = CollectionProducts(result);
        CollectionAssert.AreEquivalent(new[] { 2, 3 }, products.Select(product => product.Id).ToArray());
        Assert.AreEqual("Collection", ((ViewResult)result).ViewName);
        Assert.AreEqual("Новинки", controller.ViewData["Title"]);
        service.Verify(query => query.GetHomeAsync(token), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task New_ReturnsEmptyCollectionWhenNoNewModels()
    {
        var service = QueryService(new HomePageViewModel
        {
            Products = [new ProductCardViewModel { Id = 1, IsPopular = true, IsNew = false }]
        }, CancellationToken.None);
        var controller = new CatalogController(service.Object);

        var result = await controller.New();

        Assert.IsEmpty(CollectionProducts(result));
        service.Verify(query => query.GetHomeAsync(CancellationToken.None), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Hits_ReturnsOnlyPopularModelsAndPassesToken()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var service = QueryService(MixedProducts(), token);
        var controller = new CatalogController(service.Object);

        var result = await controller.Hits(token);

        var products = CollectionProducts(result);
        CollectionAssert.AreEquivalent(new[] { 1, 3 }, products.Select(product => product.Id).ToArray());
        Assert.AreEqual("Collection", ((ViewResult)result).ViewName);
        Assert.AreEqual("Хиты", controller.ViewData["Title"]);
        service.Verify(query => query.GetHomeAsync(token), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Hits_ReturnsEmptyCollectionWhenNoPopularModels()
    {
        var service = QueryService(new HomePageViewModel
        {
            Products = [new ProductCardViewModel { Id = 2, IsPopular = false, IsNew = true }]
        }, CancellationToken.None);
        var controller = new CatalogController(service.Object);

        var result = await controller.Hits();

        Assert.IsEmpty(CollectionProducts(result));
        service.Verify(query => query.GetHomeAsync(CancellationToken.None), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [TestMethod]
    public void Index_RedirectsToHomeModelsWithoutRequestingData()
    {
        var service = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        var controller = new CatalogController(service.Object);

        var result = controller.Index();

        Assert.IsInstanceOfType<RedirectToActionResult>(result);
        var redirect = (RedirectToActionResult)result;
        Assert.AreEqual("Home", redirect.ControllerName);
        Assert.AreEqual("Index", redirect.ActionName);
        Assert.AreEqual("catalog", redirect.Fragment);
        service.VerifyNoOtherCalls();
    }

    private static HomePageViewModel MixedProducts() => new()
    {
        Products =
        [
            new ProductCardViewModel { Id = 1, IsPopular = true, IsNew = false },
            new ProductCardViewModel { Id = 2, IsPopular = false, IsNew = true },
            new ProductCardViewModel { Id = 3, IsPopular = true, IsNew = true },
            new ProductCardViewModel { Id = 4, IsPopular = false, IsNew = false }
        ]
    };

    private static Mock<ICatalogQueryService> QueryService(HomePageViewModel model, CancellationToken token)
    {
        var service = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        service.Setup(query => query.GetHomeAsync(token)).ReturnsAsync(model);
        return service;
    }

    private static IReadOnlyList<ProductCardViewModel> CollectionProducts(IActionResult result)
    {
        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.IsInstanceOfType<IReadOnlyList<ProductCardViewModel>>(((ViewResult)result).Model);
        return (IReadOnlyList<ProductCardViewModel>)((ViewResult)result).Model!;
    }
}
