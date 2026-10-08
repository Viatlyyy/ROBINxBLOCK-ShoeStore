using Microsoft.AspNetCore.Mvc;
using ShoeStore.Controllers;
using ShoeStore.Models;

namespace ShoeStore.Tests.Unit;

[TestClass]
public class THomeController
{
    [TestMethod]
    public void Index_ReturnsViewWithDemoModel()
    {
        // Вызываем настоящее действие без веб-сервера и без подключения к БД.
        var controller = new HomeController();

        var result = controller.Index();

        Assert.IsInstanceOfType<ViewResult>(result);
        var view = (ViewResult)result;
        Assert.IsInstanceOfType<HomePageViewModel>(view.Model);
        var model = (HomePageViewModel)view.Model!;
        Assert.IsNotEmpty(model.Products);
        Assert.IsNotEmpty(model.Brands);
    }
}
