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
    public async Task Index_ReturnsHomeModelFromService()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var model = new HomePageViewModel
        {
            Products = [new ProductCardViewModel { Id = 7, Name = "9060", BrandName = "New Balance", Price = 14990m }],
            Brands = ["New Balance"]
        };
        var catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        catalog.Setup(service => service.GetHomeAsync(token)).ReturnsAsync(model);
        var controller = new HomeController(catalog.Object);

        var result = await controller.Index(token);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.IsInstanceOfType<HomePageViewModel>(((ViewResult)result).Model);
        Assert.AreSame(model, ((ViewResult)result).Model);
        // Проверяем настоящий контроллер: он должен запросить данные ровно один раз с исходным токеном.
        catalog.Verify(service => service.GetHomeAsync(token), Times.Once);
        catalog.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Index_EmptyProducts_ReturnsModelWithoutError()
    {
        var model = new HomePageViewModel { Products = [], Brands = [] };
        var catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        catalog.Setup(service => service.GetHomeAsync(CancellationToken.None)).ReturnsAsync(model);
        var controller = new HomeController(catalog.Object);

        var result = await controller.Index();

        Assert.IsInstanceOfType<ViewResult>(result);
        var returned = ((ViewResult)result).Model as HomePageViewModel;
        Assert.IsNotNull(returned);
        Assert.AreSame(model, returned);
        Assert.IsEmpty(returned.Products);
        Assert.IsEmpty(returned.Brands);
        catalog.Verify(service => service.GetHomeAsync(CancellationToken.None), Times.Once);
        catalog.VerifyNoOtherCalls();
    }
}
