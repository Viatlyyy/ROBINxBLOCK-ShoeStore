using System.Net;
using System.Security.Claims;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShoeStore.Data;
using ShoeStore.Models;

namespace ShoeStore.Tests.Integration;

// Проверяем цепочку целиком: HTTP-запрос к странице, контроллер, Identity и настоящая PostgreSQL.
// Браузер не запускается: HttpClient отправляет запросы тестовому серверу внутри процесса.
[TestClass]
[TestCategory("Integration")]
public class TRegisterIntegration
{
    // Один корректный пароль используется во всех сценариях этого класса.
    private const string Password = "TestPass1!";
    // Общая временная БД и фабрика приложения для всех тестов класса.
    // null! убирает предупреждение компилятора: объект будет создан в Initialize.
    private static PostgreSqlFixture fixture = null!;

    // ClassInitialize вызывается один раз перед тестами класса; _ — неиспользуемый TestContext.
    [ClassInitialize]
    public static async Task Initialize(TestContext _)
    {
        // Без явно заданного тестового подключения базу не создаём.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlFixture.ConnectionVariable)))
            return;
        fixture = new PostgreSqlFixture();
        // Дожидаемся создания временной базы и подготовки приложения к проверкам.
        await fixture.InitializeAsync();
    }

    // Проверка перед каждым тестом: отсутствие подключения нельзя считать успешным результатом.
    [TestInitialize]
    public void RequirePostgreSql()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlFixture.ConnectionVariable)))
            // Inconclusive отмечает тест как пропущенный: условия для его выполнения не настроены.
            Assert.Inconclusive("Для проверки PostgreSQL задайте SHOESTORE_TEST_POSTGRES. Инструкция: Tests/README.md.");
    }

    // После тестов класса освобождаем приложение и удаляем только созданную ими временную БД.
    [ClassCleanup]
    public static async Task Cleanup()
    {
        if (fixture is not null) await fixture.DisposeAsync();
    }

    [TestMethod]
    public async Task Registration_CreatesCustomerAndShowsSuccess()
    {
        // using var освобождает клиент и HTTP-ответы после выхода из метода.
        using var client = CreateClient();
        // Для каждой регистрации создаём новый e-mail, чтобы сценарии не мешали друг другу.
        var email = NewEmail();
        using var response = await RegisterAsync(client, email);

        // Успешная регистрация возвращает HTTP 302 — перенаправление на страницу регистрации.
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location);
        // Переходим по адресу из заголовка Location вручную, чтобы проверить новую страницу.
        using var page = await client.GetAsync(response.Headers.Location);
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
        // AngleSharp разбирает HTML; QuerySelector находит элемент по CSS-селектору.
        var document = await new HtmlParser().ParseDocumentAsync(await page.Content.ReadAsStringAsync());
        Assert.IsNotNull(document.QuerySelector(".account-page--register"));
        Assert.AreEqual("Регистрация успешно завершена.", document.QuerySelector("[role='status']")?.TextContent);

        // Отдельная область служб даёт контекст БД того же тестового приложения.
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // SingleAsync требует ровно одну запись с этим e-mail и читает её из PostgreSQL.
        var user = await database.Users.SingleAsync(user => user.Email == email);
        Assert.AreEqual(email, user.UserName);
        // UserManager здесь настоящий, не Moq: читаем сохранённые роль и имя пользователя.
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = await users.GetRolesAsync(user);
        CollectionAssert.AreEqual(new[] { "Customer" }, roles.ToArray());
        var claims = await users.GetClaimsAsync(user);
        // GivenName — тип claim, в котором регистрация сохраняет имя.
        Assert.AreEqual("Виталий", claims.Single(claim => claim.Type == ClaimTypes.GivenName).Value);
    }

    [TestMethod]
    public async Task Registration_StoresPasswordHash()
    {
        // Регистрируем пользователя обычной отправкой формы, затем читаем его из тестовой БД.
        using var client = CreateClient();
        var email = NewEmail();
        using var response = await RegisterAsync(client, email);
        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await database.Users.SingleAsync(user => user.Email == email);
        // Хеш должен существовать и отличаться от исходного пароля.
        // Это хеширование, а не обратимое шифрование: извлечь пароль из хеша нельзя.
        Assert.IsFalse(string.IsNullOrWhiteSpace(user.PasswordHash));
        Assert.AreNotEqual(Password, user.PasswordHash);
        // Проверяем, что хеш подходит правильному паролю и не подходит неправильному.
        Assert.IsTrue(await users.CheckPasswordAsync(user, Password));
        Assert.IsFalse(await users.CheckPasswordAsync(user, "WrongPass1!"));
    }

    [TestMethod]
    public async Task DuplicateEmail_DoesNotCreateSecondUser()
    {
        using var client = CreateClient();
        var email = NewEmail();
        using var firstResponse = await RegisterAsync(client, email);
        Assert.AreEqual(HttpStatusCode.Redirect, firstResponse.StatusCode);

        // Повторно отправляем тот же e-mail, чтобы проверить реальный запрет дубликатов.
        using var duplicateResponse = await RegisterAsync(client, email);

        // При ошибке возвращается форма с HTTP 200, а не перенаправление об успешной регистрации.
        Assert.AreEqual(HttpStatusCode.OK, duplicateResponse.StatusCode);
        var document = await new HtmlParser().ParseDocumentAsync(await duplicateResponse.Content.ReadAsStringAsync());
        // StringAssert.Contains ищет сообщение внутри текста блока ошибок.
        StringAssert.Contains(document.QuerySelector(".account-validation")?.TextContent ?? "",
            "Пользователь с таким адресом электронной почты уже зарегистрирован.");
        // Не должно быть сообщения об успехе; в БД должна остаться только первая запись.
        Assert.IsNull(document.QuerySelector("[role='status']"));
        using var scope = fixture.Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.AreEqual(1, await database.Users.CountAsync(user => user.Email == email));
    }

    // Клиент работает с тестовым сервером, а не с уже запущенным сайтом на компьютере.
    private HttpClient CreateClient() => fixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        // Это базовый адрес для относительных путей запросов внутри тестового сервера.
        BaseAddress = new Uri("http://localhost"),
        // Не следуем перенаправлению автоматически: тест должен увидеть исходный HTTP 302.
        AllowAutoRedirect = false,
        // Сохраняем cookie между запросами: это нужно для защитного токена формы и TempData.
        HandleCookies = true
    });

    // Guid создаёт случайный идентификатор; формат N записывает его без дефисов.
    private static string NewEmail() => $"registration-{Guid.NewGuid():N}@example.test";

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email)
    {
        // Сначала получаем форму, как это делает пользователь перед нажатием кнопки.
        using var page = await client.GetAsync("/Account/Register");
        Assert.AreEqual(HttpStatusCode.OK, page.StatusCode);
        var document = await new HtmlParser().ParseDocumentAsync(await page.Content.ReadAsStringAsync());
        // Читаем скрытый anti-forgery токен: сервер требует его вместе с cookie для POST.
        var token = document.QuerySelector("input[name='__RequestVerificationToken']")?.GetAttribute("value");
        Assert.IsFalse(string.IsNullOrWhiteSpace(token));
        // FormUrlEncodedContent кодирует поля так же, как отправка обычной HTML-формы.
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["FirstName"] = "Виталий",
            ["Email"] = email,
            ["Password"] = Password,
            ["ConfirmPassword"] = Password,
            ["AcceptTerms"] = "true",
            // ! здесь только подавляет предупреждение о null; наличие токена проверено выше.
            ["__RequestVerificationToken"] = token!
        });
        // Отправляем форму настоящему методу регистрации и возвращаем ответ для проверки.
        return await client.PostAsync("/Account/Register", form);
    }
}
