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
    [DataRow(1)]
    [DataRow(3)]
    public async Task Index_ReturnsCatalogModelAndForwardsPageAndToken(int page)
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var model = new CatalogPageViewModel
        {
            Products = [new ProductCardViewModel { Id = 11, Name = "Dunk Low", BrandName = "Nike", Price = 12990m }],
            Page = 2,
            TotalPages = 4
        };
        var catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        catalog.Setup(service => service.GetCatalogAsync(page, token)).ReturnsAsync(model);
        var controller = new CatalogController(catalog.Object);

        var result = await controller.Index(page, token);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.IsInstanceOfType<CatalogPageViewModel>(((ViewResult)result).Model);
        // Модель сервиса, включая пагинацию, должна попасть в представление без замены или потери данных.
        Assert.AreSame(model, ((ViewResult)result).Model);
        catalog.Verify(service => service.GetCatalogAsync(page, token), Times.Once);
        catalog.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Index_WithoutArguments_RequestsFirstPage()
    {
        var model = new CatalogPageViewModel { Page = 1, TotalPages = 1 };
        var catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        catalog.Setup(service => service.GetCatalogAsync(1, CancellationToken.None)).ReturnsAsync(model);
        var controller = new CatalogController(catalog.Object);

        var result = await controller.Index();

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(model, ((ViewResult)result).Model);
        catalog.Verify(service => service.GetCatalogAsync(1, CancellationToken.None), Times.Once);
        catalog.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task Index_EmptyProducts_ReturnsModelWithoutError()
    {
        var model = new CatalogPageViewModel { Products = [], Page = 1, TotalPages = 1 };
        var catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        catalog.Setup(service => service.GetCatalogAsync(1, CancellationToken.None)).ReturnsAsync(model);
        var controller = new CatalogController(catalog.Object);

        var result = await controller.Index(1, CancellationToken.None);

        Assert.IsInstanceOfType<ViewResult>(result);
        var returned = ((ViewResult)result).Model as CatalogPageViewModel;
        Assert.IsNotNull(returned);
        Assert.AreSame(model, returned);
        Assert.IsEmpty(returned.Products);
        catalog.Verify(service => service.GetCatalogAsync(1, CancellationToken.None), Times.Once);
        catalog.VerifyNoOtherCalls();
    }
}
