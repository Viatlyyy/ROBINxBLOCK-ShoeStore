using Microsoft.AspNetCore.Mvc;
using Moq;
using ShoeStore.Controllers;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Mocks;

[TestClass]
public class THomeController
{
    [TestMethod]
    public async Task Index_PassesCancellationTokenAndReturnsServiceModel()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var expected = new HomePageViewModel
        {
            Products = [new ProductCardViewModel { Id = 42, Name = "Test model", BrandName = "Test brand", Price = 1234m }],
            Brands = ["Test brand"]
        };
        var service = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        service.Setup(query => query.GetHomeAsync(token)).ReturnsAsync(expected);
        var controller = new HomeController(service.Object);

        var result = await controller.Index(token);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(expected, ((ViewResult)result).Model);
        service.Verify(query => query.GetHomeAsync(token), Times.Once);
        service.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Index_PassesEmptyProductsAndBrandsToView()
    {
        var expected = new HomePageViewModel();
        var service = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        service.Setup(query => query.GetHomeAsync(CancellationToken.None)).ReturnsAsync(expected);
        var controller = new HomeController(service.Object);

        var result = await controller.Index();

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(expected, ((ViewResult)result).Model);
        Assert.IsEmpty(((HomePageViewModel)((ViewResult)result).Model!).Products);
        Assert.IsEmpty(((HomePageViewModel)((ViewResult)result).Model!).Brands);
        service.Verify(query => query.GetHomeAsync(CancellationToken.None), Times.Once);
        service.VerifyNoOtherCalls();
    }
}
