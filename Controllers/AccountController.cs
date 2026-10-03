using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using ShoeStore.Models;

namespace ShoeStore.Controllers;

public class AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn) : Controller
{
    public IActionResult AccessDenied() => View();

    // Авторизованному пользователю форму входа повторно не показываем.
    public IActionResult Login() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View();

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("authentication")]
    public async Task<IActionResult> Login(LoginViewModel vm)
    {
        // MVC уже проверил атрибуты модели. Возвращаем введённые данные и ошибки в ту же форму.
        if (!ModelState.IsValid) return View(vm);

        // Ищем по email. Пробелы по краям убираем, но сам пароль не меняем.
        var user = await users.FindByEmailAsync(vm.Email.Trim());
        if (user is null)
        {
            ModelState.AddModelError("", "Пользователь с таким адресом электронной почты не зарегистрирован.");
            return View(vm);
        }

        // false — без постоянной cookie, true — неверный пароль учитывается для блокировки.
        // Проверку хеша оставляем Identity, вручную пароль с данными из базы не сравниваем.
        var result = await signIn.PasswordSignInAsync(user, vm.Password, false, true);
        // После успешного входа открываем главную страницу.
        if (result.Succeeded)
            return RedirectToAction("Index", "Home");

        // При неудачном входе показываем причину ошибки в форме.
        ModelState.AddModelError("", result.IsLockedOut
            ? "Слишком много неудачных попыток. Повторите вход через 15 минут."
            : "Неверный пароль.");
        return View(vm);
    }

    // Показываем форму регистрации.
    public IActionResult Register() => View();

    // Обрабатываем данные отправленной формы.
    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("authentication")]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        // Если поля заполнены неправильно, возвращаем форму с ошибками.
        if (!ModelState.IsValid) return View(vm);

        // Убираем лишние пробелы по краям email и имени.
        var email = vm.Email.Trim();
        var firstName = vm.FirstName.Trim();
        var user = new ApplicationUser { UserName = email, Email = email };
        // Identity создаёт аккаунт и сохраняет хеш пароля.
        var result = await users.CreateAsync(user, vm.Password);
        if (result.Succeeded)
        {
            // Назначаем новому пользователю роль покупателя.
            var roleResult = await users.AddToRoleAsync(user, "Customer");
            if (!roleResult.Succeeded)
            {
                // Если роль не назначена, удаляем созданный аккаунт и показываем ошибки.
                await users.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                return View(vm);
            }

            // Сохраняем имя в дополнительных данных пользователя (claim).
            var nameResult = await users.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, firstName));
            if (!nameResult.Succeeded)
            {
                // Если имя не сохранилось, удаляем созданный аккаунт и показываем ошибки.
                await users.DeleteAsync(user);
                AddIdentityErrors(nameResult);
                return View(vm);
            }

            // Передаём сообщение об успехе и снова открываем форму регистрации.
            TempData["Message"] = "Регистрация успешно завершена.";
            return RedirectToAction(nameof(Register));
        }

        // Если аккаунт не создан, возвращаем форму с ошибками Identity.
        AddIdentityErrors(result);
        return View(vm);
    }

    // Выход только через POST с токеном, а не переходом по случайной ссылке.
    [HttpPost, Authorize, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // Добавляем ошибки Identity в форму, чтобы пользователь их увидел.
    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError("", RussianIdentityError(error.Code));
    }

    // По коду ошибки выбираем сообщение на русском языке.
    private static string RussianIdentityError(string code) => code switch
    {
        "ConcurrencyFailure" => "Не удалось сохранить изменения: данные были изменены другим пользователем. Обновите страницу и повторите попытку.",
        "PasswordMismatch" => "Неверный пароль.",
        "InvalidToken" => "Недействительный или устаревший токен подтверждения.",
        "RecoveryCodeRedemptionFailed" => "Не удалось использовать код восстановления.",
        "LoginAlreadyAssociated" => "Этот способ входа уже привязан к другой учётной записи.",
        "InvalidUserName" => "Имя пользователя содержит недопустимые символы.",
        "InvalidEmail" => "Укажите корректный адрес электронной почты.",
        "DuplicateUserName" or "DuplicateEmail" => "Пользователь с таким адресом электронной почты уже зарегистрирован.",
        "InvalidRoleName" => "Название роли содержит недопустимые символы.",
        "DuplicateRoleName" => "Роль с таким названием уже существует.",
        "UserAlreadyHasPassword" => "Для этой учётной записи уже задан пароль.",
        "UserLockoutNotEnabled" => "Для этой учётной записи недоступна блокировка.",
        "UserAlreadyInRole" => "Пользователь уже состоит в указанной роли.",
        "UserNotInRole" => "Пользователь не состоит в указанной роли.",
        "PasswordTooShort" => "Пароль должен содержать не менее 8 символов.",
        "PasswordRequiresUniqueChars" => "Пароль должен содержать достаточное количество различных символов.",
        "PasswordRequiresNonAlphanumeric" => "Пароль должен содержать хотя бы один специальный символ (!, ?, @, #, $, %, &, *, _, -).",
        "PasswordRequiresDigit" => "Пароль должен содержать хотя бы одну цифру.",
        "PasswordRequiresLower" => "Пароль должен содержать хотя бы одну строчную букву.",
        "PasswordRequiresUpper" => "Пароль должен содержать хотя бы одну заглавную букву.",
        _ => "Во время обработки запроса произошла ошибка. Повторите попытку позже."
    };
}
