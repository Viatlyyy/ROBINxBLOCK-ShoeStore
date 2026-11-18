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
        var expected = new HomePageViewModel
        {
            Products = [new ProductCardViewModel { Id = 7001, Name = "Модель из базы", Price = 12345m }],
            Brands = ["Тестовый бренд"]
        };
        var catalog = new Mock<ICatalogQueryService>();
        catalog.Setup(service => service.GetHomeAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);
        var controller = new HomeController(catalog.Object);

        var result = (ViewResult)await controller.Index();

        Assert.AreSame(expected, result.Model);
    }
}
