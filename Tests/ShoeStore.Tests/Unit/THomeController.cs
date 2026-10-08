using Microsoft.AspNetCore.Mvc;
using Moq;
using ShoeStore.Controllers;
using ShoeStore.Models;
using ShoeStore.Services;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace ShoeStore.Tests.Unit;

[TestClass]
public class THomeController
{
    [TestMethod]
    [TestCategory("CatalogTiles")]
    public async Task Index_ReturnsViewWithCatalogModel()
    {
        var catalog = new Mock<ICatalogQueryService>();
        catalog.Setup(service => service.GetHomeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HomePageViewModel
            {
                Products = [new ProductCardViewModel { Id = 7001, Name = "Модель из базы", BrandName = "Тестовый бренд", Price = 12345m }],
                Brands = ["Тестовый бренд"]
            });
        var controller = new HomeController(catalog.Object);

        var result = await controller.Index();

        Assert.IsInstanceOfType<ViewResult>(result);
        var view = (ViewResult)result;
        Assert.IsInstanceOfType<HomePageViewModel>(view.Model);
        var model = (HomePageViewModel)view.Model!;
        Assert.HasCount(1, model.Products);
        Assert.AreEqual("Модель из базы", model.Products[0].Name);
        Assert.AreEqual("Тестовый бренд", model.Products[0].BrandName);
        Assert.AreEqual(12345m, model.Products[0].Price);
        Assert.IsNotEmpty(model.Brands);
    }
}
