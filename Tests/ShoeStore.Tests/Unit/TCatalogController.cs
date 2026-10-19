using Microsoft.AspNetCore.Mvc;
using Moq;
using ShoeStore.Controllers;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Unit;

[TestClass]
[TestCategory("ProductDetails")]
public class TCatalogController
{
    [TestMethod]
    public void Details_ReturnsSelectedProductModel()
    {
        Assert.AreEqual(typeof(IActionResult), typeof(CatalogController).GetMethod("Details")?.ReturnType);

        var products = new[]
        {
            new ProductCardViewModel
            {
                Id = 101, Name = "Учебная модель", BrandName = "Тестовый бренд", Description = "Описание первого товара",
                Price = 12345m, ImageUrl = "/images/products/9060.jpg",
                GalleryImageUrls = ["/images/products/9060.jpg", "/images/products/2002r.jpg"]
            },
            new ProductCardViewModel
            {
                Id = 102, Name = "Другой товар", BrandName = "Другой бренд", Description = "Описание второго товара",
                Price = 23456m, ImageUrl = "/images/products/530.jpg",
                GalleryImageUrls = ["/images/products/530.jpg", "/images/products/574-legacy.jpg"]
            }
        };
        var catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        foreach (var product in products)
            catalog.Setup(service => service.GetProduct(product.Id)).Returns(product);
        var controller = new CatalogController(catalog.Object);

        foreach (var product in products)
        {
            var result = controller.Details(product.Id) as ViewResult;
            Assert.IsNotNull(result);
            Assert.AreSame(product, result.Model);
        }
    }
}
