using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ShoeStore.Models;
using ShoeStore.Services;

namespace ShoeStore.Tests.Integration;

[TestClass]
[TestCategory("ProductOptions")]
public class TProductOptionsIntegration
{
    private ProductCardViewModel[] products = [];
    private Mock<ICatalogQueryService> catalog = null!;
    private WebApplicationFactory<Program> application = null!;
    private HttpClient client = null!;

    [TestInitialize]
    public void Setup()
    {
        products =
        [
            new ProductCardViewModel
            {
                Id = 101, Name = "Первый товар", BrandName = "Первый бренд", Price = 12000m,
                Sizes =
                [
                    new ProductSizeViewModel { Id = 1001, Size = 36, StockQuantity = 1 },
                    new ProductSizeViewModel { Id = 1002, Size = 37, StockQuantity = 3 },
                    new ProductSizeViewModel { Id = 1003, Size = 38, StockQuantity = 10 },
                    new ProductSizeViewModel { Id = 1004, Size = 39, StockQuantity = 25 }
                ]
            },
            new ProductCardViewModel
            {
                Id = 102, Name = "Второй товар", BrandName = "Второй бренд", Price = 15000m,
                Sizes =
                [
                    new ProductSizeViewModel { Id = 2001, Size = 40, StockQuantity = 2 },
                    new ProductSizeViewModel { Id = 2002, Size = 41, StockQuantity = 12 }
                ]
            }
        ];
        catalog = new Mock<ICatalogQueryService>(MockBehavior.Strict);
        foreach (var product in products)
            catalog.Setup(service => service.GetProductAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        application = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Host=127.0.0.1;Port=1;Database=unused_product_options;Username=unused;Timeout=1");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICatalogQueryService>();
                services.AddSingleton(catalog.Object);
                var roles = new Mock<RoleManager<IdentityRole>>(
                    Mock.Of<IRoleStore<IdentityRole>>(), Array.Empty<IRoleValidator<IdentityRole>>(),
                    new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
                    NullLogger<RoleManager<IdentityRole>>.Instance);
                roles.Setup(manager => manager.RoleExistsAsync("Customer")).ReturnsAsync(true);
                services.RemoveAll<RoleManager<IdentityRole>>();
                services.AddSingleton(roles.Object);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });
        client = application.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        client?.Dispose();
        application?.Dispose();
    }

    [TestMethod]
    public async Task Details_DisplaysSizesForSelectedProduct()
    {
        foreach (var product in products)
        {
            var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Catalog/Details/{product.Id}"));
            var sizes = Regex.Match(html, "<div class=\"sizes\"[^>]*>(?<sizes>[\\s\\S]*?)</div>").Groups["sizes"].Value;
            var choices = Regex.Matches(sizes, "<input required type=\"radio\" name=\"size\" value=\"(\\d+)\" data-stock=\"(\\d+)\"");
            CollectionAssert.AreEqual(product.Sizes.Select(size => size.Size).ToArray(),
                choices.Select(choice => int.Parse(choice.Groups[1].Value)).ToArray());
            CollectionAssert.AreEqual(product.Sizes.Select(size => size.StockQuantity).ToArray(),
                choices.Select(choice => int.Parse(choice.Groups[2].Value)).ToArray());
        }
    }

    [TestMethod]
    public async Task Details_InitializesSizeAndQuantityControls()
    {
        foreach (var product in products)
        {
            var html = await client.GetStringAsync($"/Catalog/Details/{product.Id}");
            var choices = Regex.Matches(html, "<input[^>]*type=\"radio\"[^>]*>");
            Assert.AreEqual(product.Sizes.Count, choices.Count);
            Assert.IsFalse(choices.Any(choice => choice.Value.Contains("checked")));
            StringAssert.Contains(html, "/Cart/Add");
            var input = Regex.Match(html, "<input\\b[^>]*data-quantity-input[^>]*>").Value;
            StringAssert.Contains(input, "value=\"1\"");
            Assert.IsTrue(Regex.IsMatch(input, "\\breadonly(?:\\s|=|/?>)"));
            foreach (var direction in new[] { "decrease", "increase" })
            {
                var button = Regex.Match(html, $"<button\\b[^>]*data-quantity-{direction}[^>]*>").Value;
                Assert.IsTrue(Regex.IsMatch(button, "\\bdisabled(?:\\s|=|>)"));
            }
            StringAssert.Contains(html, "data-quantity-limit=\"10\"");
            StringAssert.Contains(html, "/js/product-options.js");
        }
    }

    [TestMethod]
    public async Task Details_DisablesSelectionWhenNoSizesAreAvailable()
    {
        catalog.Setup(service => service.GetProductAsync(101, It.IsAny<CancellationToken>())).ReturnsAsync(new ProductCardViewModel
        {
            Id = 101, Name = "Товар без доступных размеров", BrandName = "Первый бренд", Price = 12000m
        });
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog/Details/101"));
        var sizes = Regex.Match(html, "<div class=\"sizes\"[^>]*>(?<sizes>[\\s\\S]*?)</div>").Groups["sizes"].Value;

        StringAssert.Contains(sizes, "Нет доступных размеров");
        Assert.IsFalse(sizes.Contains("type=\"radio\""));
        var input = Regex.Match(html, "<input\\b[^>]*data-quantity-input[^>]*>").Value;
        StringAssert.Contains(input, "value=\"1\"");
        foreach (var direction in new[] { "decrease", "increase" })
        {
            var button = Regex.Match(html, $"<button\\b[^>]*data-quantity-{direction}[^>]*>").Value;
            Assert.IsTrue(Regex.IsMatch(button, "\\bdisabled(?:\\s|=|>)"));
        }
    }
}
