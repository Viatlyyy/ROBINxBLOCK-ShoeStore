using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using ShoeStore.Models;

namespace ShoeStore.Controllers;

public class AccountController(UserManager<ApplicationUser> users) : Controller
{
    public IActionResult Register() => View();

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("authentication")]
    public async Task<IActionResult> Register(RegisterViewModel vm)
    {
        if (!ModelState.IsValid) return View(vm);

        var email = vm.Email.Trim();
        var firstName = vm.FirstName.Trim();
        var user = new ApplicationUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user, vm.Password);
        if (result.Succeeded)
        {
            var roleResult = await users.AddToRoleAsync(user, "Customer");
            if (!roleResult.Succeeded)
            {
                await users.DeleteAsync(user);
                AddIdentityErrors(roleResult);
                return View(vm);
            }

            var nameResult = await users.AddClaimAsync(user, new Claim(ClaimTypes.GivenName, firstName));
            if (!nameResult.Succeeded)
            {
                await users.DeleteAsync(user);
                AddIdentityErrors(nameResult);
                return View(vm);
            }

            TempData["Message"] = "Регистрация успешно завершена.";
            return RedirectToAction(nameof(Register));
        }

        AddIdentityErrors(result);
        return View(vm);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
            ModelState.AddModelError("", RussianIdentityError(error.Code));
    }

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
