using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ShoeStore.Controllers;
using ShoeStore.Models;

namespace ShoeStore.Tests.Mocks;


[TestClass]
public class TAccountController
{

    private Mock<UserManager<ApplicationUser>> users = null!;
    private Mock<SignInManager<ApplicationUser>> signIn = null!;
    private AccountController controller = null!;


    [TestInitialize]
    public void Setup()
    {


        users = new Mock<UserManager<ApplicationUser>>(
            Mock.Of<IUserStore<ApplicationUser>>(), null!, null!, null!, null!, null!, null!, null!, null!);
        signIn = new Mock<SignInManager<ApplicationUser>>(
            users.Object, Mock.Of<IHttpContextAccessor>(), Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);

        controller = new AccountController(users.Object, signIn.Object);
    }

    [TestMethod]
    public async Task Login_ValidCredentials_RedirectsToHome()
    {

        var model = new LoginViewModel { Email = " user@example.com ", Password = "Password1!" };
        var user = new ApplicationUser { Email = "user@example.com", UserName = "user@example.com" };

        users.Setup(manager => manager.FindByEmailAsync("user@example.com")).ReturnsAsync(user);
        signIn.Setup(manager => manager.PasswordSignInAsync(user, model.Password, false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);


        var result = await controller.Login(model);


        Assert.IsInstanceOfType<RedirectToActionResult>(result);
        var redirect = (RedirectToActionResult)result;
        Assert.AreEqual("Index", redirect.ActionName);
        Assert.AreEqual("Home", redirect.ControllerName);
        Assert.IsTrue(controller.ModelState.IsValid);
    }

    [TestMethod]
    public async Task Login_UnknownEmail_ReturnsError()
    {

        var model = new LoginViewModel { Email = "missing@example.com", Password = "Password1!" };
        users.Setup(manager => manager.FindByEmailAsync(model.Email)).ReturnsAsync((ApplicationUser?)null);


        var result = await controller.Login(model);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(model, ((ViewResult)result).Model);


        Assert.AreEqual("Пользователь с таким адресом электронной почты не зарегистрирован.",
            controller.ModelState[""]!.Errors.Single().ErrorMessage);


        signIn.Verify(manager => manager.PasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        signIn.Verify(manager => manager.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task Login_WrongPassword_ReturnsError()
    {

        var model = new LoginViewModel { Email = "user@example.com", Password = "WrongPassword1!" };
        var user = new ApplicationUser { Email = model.Email, UserName = model.Email };
        users.Setup(manager => manager.FindByEmailAsync(model.Email)).ReturnsAsync(user);
        signIn.Setup(manager => manager.PasswordSignInAsync(user, model.Password, false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);


        var result = await controller.Login(model);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(model, ((ViewResult)result).Model);

        Assert.AreEqual("Неверный пароль.", controller.ModelState[""]!.Errors.Single().ErrorMessage);
    }


    [TestMethod]
    public async Task Login_LockedOutUser_ReturnsError()
    {
        var model = new LoginViewModel { Email = "user@example.com", Password = "Password1!" };
        var user = new ApplicationUser { Email = model.Email, UserName = model.Email };
        users.Setup(manager => manager.FindByEmailAsync(model.Email)).ReturnsAsync(user);
        signIn.Setup(manager => manager.PasswordSignInAsync(user, model.Password, false, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);


        var result = await controller.Login(model);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(model, ((ViewResult)result).Model);

        Assert.AreEqual("Слишком много неудачных попыток. Повторите вход через 15 минут.",
            controller.ModelState[""]!.Errors.Single().ErrorMessage);
    }

    [TestMethod]
    public async Task Login_InvalidModel_ReturnsForm()
    {
        var model = new LoginViewModel();

        controller.ModelState.AddModelError(nameof(model.Email), "Введите адрес электронной почты.");


        var result = await controller.Login(model);

        Assert.IsInstanceOfType<ViewResult>(result);
        Assert.AreSame(model, ((ViewResult)result).Model);

        users.Verify(manager => manager.FindByEmailAsync(It.IsAny<string>()), Times.Never);

        signIn.Verify(manager => manager.PasswordSignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        signIn.Verify(manager => manager.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    public async Task Logout_SignsOutAndRedirectsToHome()
    {

        signIn.Setup(manager => manager.SignOutAsync()).Returns(Task.CompletedTask);


        var result = await controller.Logout();


        Assert.IsInstanceOfType<RedirectToActionResult>(result);
        var redirect = (RedirectToActionResult)result;
        Assert.AreEqual("Index", redirect.ActionName);
        Assert.AreEqual("Home", redirect.ControllerName);

        signIn.Verify(manager => manager.SignOutAsync(), Times.Once);
    }
}
