using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ShoeStore.Controllers;
using ShoeStore.Models;

namespace ShoeStore.Tests.Mocks;

// Проверяем решения контроллера и сообщения формы; работу PostgreSQL и алгоритм хеширования здесь не запускаем.
[TestClass]
public class TAccountController
{
    // Поля заполняет Setup перед каждым тестом; null! снимает предупреждение о начальном значении.
    private Mock<UserManager<ApplicationUser>> users = null!;
    private Mock<SignInManager<ApplicationUser>> signIn = null!;
    private AccountController controller = null!;

    // Каждый метод получает новые моки: чужие настройки и старые вызовы не должны влиять на результат.
    [TestInitialize]
    public void Setup()
    {

        // Хранилище и HTTP-контекст подменяем; остальные параметры не используются замоканными методами.
        users = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        signIn = new Mock<SignInManager<ApplicationUser>>(
            users.Object, Mock.Of<IHttpContextAccessor>(), Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);
        // Object даёт подмену менеджера, которую контроллер получает как обычную зависимость.
        controller = new AccountController(users.Object, signIn.Object);
    }

    [TestMethod]
    public async Task Login_ValidCredentials_RedirectsToHome()
    {
        // Пробелы вокруг email добавлены специально: перед поиском контроллер должен их убрать.
        var model = new LoginViewModel { Email = " user@example.com ", Password = "Password1!" };
        var user = new ApplicationUser { Email = "user@example.com", UserName = "user@example.com" };
        // Задаём существующего пользователя и успешную проверку пароля — это сценарий обычного входа.
        users.Setup(manager => manager.FindByEmailAsync("user@example.com")).ReturnsAsync(user);
        signIn.Setup(manager => manager.PasswordSignInAsync(user, model.Password, false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

        // Вызываем действие напрямую. В отличие от HTTP-запроса, MVC здесь не заполняет ModelState за нас.
        var result = await controller.Login(model);

        // Нужен переход именно на Home/Index: проверка только типа результата пропустила бы неверный маршрут.
        Assert.IsInstanceOfType<RedirectToActionResult>(result);
        var redirect = (RedirectToActionResult)result;
        Assert.AreEqual("Index", redirect.ActionName);
        Assert.AreEqual("Home", redirect.ControllerName);
        Assert.IsTrue(controller.ModelState.IsValid);
    }

    [TestMethod]
    public async Task Login_UnknownEmail_ReturnsError()
    {
        // По этому адресу никого нет. Пароль правильного формата, чтобы не смешивать разные причины отказа.
        var model = new LoginViewModel { Email = "missing@example.com", Password = "Password1!" };
        users.Setup(manager => manager.FindByEmailAsync(model.Email)).ReturnsAsync((ApplicationUser?)null);

        // Результат поиска null должен вернуть форму, а не попытаться войти с несуществующим аккаунтом.
        var result = await controller.Login(model);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(model, ((ViewResult)result).Model);
        // Форма сохраняет введённую модель и сообщает, что учётная запись не найдена.
        // Пустой ключ в ModelState означает общую ошибку формы; Single требует ровно одно сообщение.
        Assert.AreEqual("Пользователь с таким адресом электронной почты не зарегистрирован.",
            controller.ModelState[""]!.Errors.Single().ErrorMessage);
        // Проверяем обе перегрузки: при неизвестном email или ошибке формы до проверки пароля доходить нельзя.
        // It.IsAny разрешает любые аргументы, Times.Never требует, чтобы вызовов вообще не было.
        signIn.Verify(manager => manager.PasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        signIn.Verify(manager => manager.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task Login_WrongPassword_ReturnsError()
    {
        // Аккаунт найден, но менеджер вернёт Failed. Проверяем именно неверный пароль.
        var model = new LoginViewModel { Email = "user@example.com", Password = "WrongPassword1!" };
        var user = new ApplicationUser { Email = model.Email, UserName = model.Email };
        users.Setup(manager => manager.FindByEmailAsync(model.Email)).ReturnsAsync(user);
        signIn.Setup(manager => manager.PasswordSignInAsync(user, model.Password, false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        // Мок уже решил, что пароль неверный. Проверяем, как настоящий контроллер обработает этот ответ.
        var result = await controller.Login(model);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(model, ((ViewResult)result).Model);
        // Требуется отдельное сообщение из задачи, а не общая ошибка для всех причин отказа.
        Assert.AreEqual("Неверный пароль.", controller.ModelState[""]!.Errors.Single().ErrorMessage);
    }

    // Проверяем реакцию на LockedOut. Подсчёт пяти попыток выполняет Identity, а не этот тест.
    [TestMethod]
    public async Task Login_LockedOutUser_ReturnsError()
    {
        var model = new LoginViewModel { Email = "user@example.com", Password = "Password1!" };
        var user = new ApplicationUser { Email = model.Email, UserName = model.Email };
        users.Setup(manager => manager.FindByEmailAsync(model.Email)).ReturnsAsync(user);
        signIn.Setup(manager => manager.PasswordSignInAsync(user, model.Password, false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        // LockedOut отличается от Failed: нельзя показывать обычную ошибку пароля вместо сообщения о блокировке.
        var result = await controller.Login(model);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(model, ((ViewResult)result).Model);
        // Заблокированный аккаунт остаётся на форме и получает объяснение, когда можно попробовать снова.
        Assert.AreEqual("Слишком много неудачных попыток. Повторите вход через 15 минут.",
            controller.ModelState[""]!.Errors.Single().ErrorMessage);
    }

    [TestMethod]
    public async Task Login_InvalidModel_ReturnsForm()
    {
        var model = new LoginViewModel();
        // Имитируем ошибку, которую при обычном POST-запросе добавил бы валидатор MVC.
        controller.ModelState.AddModelError(nameof(model.Email), "Введите адрес электронной почты.");

        // Контроллер должен заметить уже заполненный ModelState и остановиться до обращения к менеджерам.
        var result = await controller.Login(model);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(model, ((ViewResult)result).Model);
        // Невалидную форму возвращаем сразу: даже поиск пользователя в базе здесь лишний.
        users.Verify(manager => manager.FindByEmailAsync(It.IsAny<string>()), Times.Never);
        // Даже если реализацию переключат на другую перегрузку, невалидная форма не должна запускать вход.
        signIn.Verify(manager => manager.PasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        signIn.Verify(manager => manager.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task Logout_SignsOutAndRedirectsToHome()
    {
        // SignOutAsync не возвращает значение. CompletedTask даёт дождаться выхода без настоящей cookie.
        signIn.Setup(manager => manager.SignOutAsync()).Returns(Task.CompletedTask);

        // Действие должно завершить сессию и отправить пользователя на главную страницу.
        var result = await controller.Logout();

        // Нужен переход именно на Home/Index: проверка только типа результата пропустила бы неверный маршрут.
        Assert.IsInstanceOfType<RedirectToActionResult>(result);
        var redirect = (RedirectToActionResult)result;
        Assert.AreEqual("Index", redirect.ActionName);
        Assert.AreEqual("Home", redirect.ControllerName);
        // Перехода недостаточно: можно забыть завершить сессию. Поэтому нужен ровно один вызов SignOutAsync.
        signIn.Verify(manager => manager.SignOutAsync(), Times.Once);
    }
}
