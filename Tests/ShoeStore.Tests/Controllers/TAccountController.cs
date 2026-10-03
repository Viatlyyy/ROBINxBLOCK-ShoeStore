using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ShoeStore.Controllers;
using ShoeStore.Models;

namespace ShoeStore.Tests.Controllers;


[TestClass]
[TestCategory("Mock")]
public class TAccountController
{

    // null! сообщает компилятору, что поля будут заполнены позже, в Initialize.
    private Mock<UserManager<ApplicationUser>> users = null!;
    private AccountController controller = null!;


    [TestInitialize]
    public void Initialize()
    {
        users = CreateUserManager();
        // Setup задаёт поведение подмены; It.IsAny принимает любое значение указанного типа.
        // ReturnsAsync возвращает успешный результат асинхронного метода без обращения к БД.
        users.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()))
            .ReturnsAsync(IdentityResult.Success);
        users.Setup(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        // Создаём HTTP-контекст вручную, потому что контроллер вызывается без веб-сервера.
        var context = new DefaultHttpContext();
        // users.Object — подмена, которую передаём контроллеру вместо обычного UserManager.
        controller = new AccountController(users.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            // TempData хранит сообщение для следующего запроса. Его хранилище здесь подменено.
            TempData = new TempDataDictionary(context, Mock.Of<ITempDataProvider>())
        };
    }

    [TestMethod]
    public async Task ValidRegistration_CreatesCustomerAndSavesName()
    {
        var form = CreateValidForm();
        // Пробелы по краям нужны, чтобы проверить обрезку имени и e-mail контроллером.
        form.FirstName = "  Виталий  ";
        form.Email = "  customer@example.com  ";

        // Task и await позволяют дождаться завершения асинхронного метода регистрации.
        var result = await controller.Register(form);

        // Проверяем перенаправление на регистрацию и сообщение об успехе в TempData.
        Assert.IsInstanceOfType<RedirectToActionResult>(result);
        Assert.AreEqual("Register", ((RedirectToActionResult)result).ActionName);
        Assert.AreEqual("Регистрация успешно завершена.", controller.TempData["Message"]);
        // Verify проверяет вызовы. It.Is задаёт условия для аргумента, Times.Once — ровно один вызов.
        // Здесь проверяем передачу e-mail в Email и UserName, а также пароля в CreateAsync.
        users.Verify(manager => manager.CreateAsync(
            It.Is<ApplicationUser>(user => user.Email == "customer@example.com"
                && user.UserName == "customer@example.com"), "TestPass1!"), Times.Once);
        // Проверяем, что созданному пользователю назначается роль покупателя Customer.
        users.Verify(manager => manager.AddToRoleAsync(
            It.Is<ApplicationUser>(user => user.Email == "customer@example.com"), "Customer"), Times.Once);
        // Имя сохраняется как claim GivenName — дополнительное свойство учётной записи.
        users.Verify(manager => manager.AddClaimAsync(
            It.Is<ApplicationUser>(user => user.Email == "customer@example.com"),
            It.Is<Claim>(claim => claim.Type == ClaimTypes.GivenName && claim.Value == "Виталий")), Times.Once);
        // Times.Never означает, что метод не должен вызываться: успешного пользователя не удаляют.
        users.Verify(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [TestMethod]
    public async Task InvalidForm_DoesNotCreateUser()
    {
        var form = CreateValidForm();
        // При HTTP-запросе MVC проверяет модель автоматически. При прямом вызове делаем это вручную.
        // Добавляем готовую ошибку, чтобы проверить ветку контроллера с неверным ModelState.
        controller.ModelState.AddModelError(nameof(form.FirstName), "Введите имя.");

        var result = await controller.Register(form);

        // ViewResult возвращает форму без перенаправления. AreSame проверяет тот же объект модели.
        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(form, ((ViewResult)result).Model);
        Assert.IsFalse(controller.ModelState.IsValid);
        Assert.IsFalse(controller.TempData.ContainsKey("Message"));
        // Невалидная форма не должна приводить к созданию пользователя, роли или имени.
        users.Verify(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        users.Verify(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        users.Verify(manager => manager.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()), Times.Never);
    }

    [TestMethod]
    // Оба кода могут означать занятый e-mail, потому что он используется и как логин.
    [DataRow("DuplicateEmail")]
    [DataRow("DuplicateUserName")]
    public async Task DuplicateEmail_ReturnsError(string code)
    {
        var form = CreateValidForm();

        users.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(CreateFailure(code));

        var result = await controller.Register(form);

        // Подмена сообщает о дубликате; проверяем сообщение и отсутствие дальнейших действий.
        // Реальное ограничение повторного e-mail в БД проверяется интеграционным тестом.
        AssertRegistrationError(result, form,
            "Пользователь с таким адресом электронной почты уже зарегистрирован.");
        users.Verify(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        users.Verify(manager => manager.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()), Times.Never);
        users.Verify(manager => manager.DeleteAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [TestMethod]
    // В каждый запуск передаются код ошибки Identity и ожидаемый текст для пользователя.
    [DataRow("PasswordRequiresUpper", "Пароль должен содержать хотя бы одну заглавную букву.")]
    [DataRow("PasswordRequiresLower", "Пароль должен содержать хотя бы одну строчную букву.")]
    [DataRow("PasswordRequiresDigit", "Пароль должен содержать хотя бы одну цифру.")]
    [DataRow("PasswordRequiresNonAlphanumeric", "Пароль должен содержать хотя бы один специальный символ (!, ?, @, #, $, %, &, *, _, -).")]
    [DataRow("UnknownError", "Во время обработки запроса произошла ошибка. Повторите попытку позже.")]
    public async Task CreateUserFailure_ReturnsRussianError(string code, string message)
    {
        var form = CreateValidForm();
        users.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(CreateFailure(code));

        var result = await controller.Register(form);

        // Здесь проверяется перевод кодов ошибок, а не реальная проверка сложности пароля.
        AssertRegistrationError(result, form, message);
        // Сообщение об ошибке не должно раскрывать введённый пароль.
        Assert.IsFalse(controller.ModelState[""]!.Errors[0].ErrorMessage.Contains(form.Password));
        users.Verify(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
        users.Verify(manager => manager.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()), Times.Never);
    }

    [TestMethod]
    public async Task RoleFailure_DeletesCreatedUser()
    {
        var form = CreateValidForm();
        // Создание пользователя успешно, но назначение роли завершается ошибкой.
        users.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Customer"))
            .ReturnsAsync(CreateFailure("UnknownError"));

        var result = await controller.Register(form);

        AssertRegistrationError(result, form, "Во время обработки запроса произошла ошибка. Повторите попытку позже.");
        // Проверяем откат: контроллер должен удалить пользователя, созданного без нужной роли.
        users.Verify(manager => manager.DeleteAsync(
            It.Is<ApplicationUser>(user => user.Email == form.Email)), Times.Once);
        users.Verify(manager => manager.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()), Times.Never);
    }

    [TestMethod]
    public async Task NameFailure_DeletesCreatedUser()
    {
        var form = CreateValidForm();
        // Имитируем отказ при сохранении имени после успешного создания пользователя и роли.
        users.Setup(manager => manager.AddClaimAsync(It.IsAny<ApplicationUser>(), It.IsAny<Claim>()))
            .ReturnsAsync(CreateFailure("UnknownError"));

        var result = await controller.Register(form);

        AssertRegistrationError(result, form, "Во время обработки запроса произошла ошибка. Повторите попытку позже.");
        // Пользователь с неполностью сохранёнными данными также должен быть удалён.
        users.Verify(manager => manager.DeleteAsync(
            It.Is<ApplicationUser>(user => user.Email == form.Email)), Times.Once);
    }

    // Общие проверки ошибочного результата вынесены сюда, чтобы не повторять их в каждом тесте.
    private void AssertRegistrationError(IActionResult result, RegisterViewModel form, string message)
    {
        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(form, ((ViewResult)result).Model);
        Assert.IsFalse(controller.ModelState.IsValid);
        // Пустой ключ в ModelState означает ошибку всей формы, а не отдельного поля.
        Assert.IsNotNull(controller.ModelState[""]);
        Assert.AreEqual(1, controller.ModelState[""]!.Errors.Count);
        Assert.AreEqual(message, controller.ModelState[""]!.Errors[0].ErrorMessage);
        Assert.IsFalse(controller.TempData.ContainsKey("Message"));
    }

    // Создаём неуспешный результат Identity. Техническое Description не должно попасть в форму.
    private static IdentityResult CreateFailure(string code) => IdentityResult.Failed(
        new IdentityError { Code = code, Description = "Technical details must not reach the form." });

    // Новая корректная форма для каждого теста; эти данные не сохраняются в настоящей БД.
    private static RegisterViewModel CreateValidForm() => new()
    {
        FirstName = "Виталий",
        Email = "customer@example.com",
        Password = "TestPass1!",
        ConfirmPassword = "TestPass1!",
        AcceptTerms = true
    };


    private static Mock<UserManager<ApplicationUser>> CreateUserManager() => new(
        // Подмена хранилища пользователей — реальной БД в этих тестах нет.
        Mock.Of<IUserStore<ApplicationUser>>(),
        // Настройки Identity, упакованные в IOptions.
        Options.Create(new IdentityOptions()),
        // Стандартный компонент хеширования; сами операции создания здесь подменены.
        new PasswordHasher<ApplicationUser>(),
        // Пустые наборы проверок пользователя и пароля для конструктора подмены.
        Array.Empty<IUserValidator<ApplicationUser>>(),
        Array.Empty<IPasswordValidator<ApplicationUser>>(),
        // Нормализатор приводит логины и e-mail к единому виду при работе Identity.
        new UpperInvariantLookupNormalizer(),
        // Стандартный поставщик описаний ошибок Identity.
        new IdentityErrorDescriber(),
        // Подмена контейнера служб и логгер, который ничего не записывает.
        Mock.Of<IServiceProvider>(),
        NullLogger<UserManager<ApplicationUser>>.Instance);
}
