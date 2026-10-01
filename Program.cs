using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using ShoeStore.Models;

// В Development автоматически загружаются User Secrets проекта ShoeStore.
// Подключение задаём там: у каждого своя база, и пароль не попадает в Git.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException(
            "Не задана строка подключения. Выполните: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...\"");
    // Npgsql связывает EF Core с PostgreSQL. Имя подключения должно совпадать с ключом в секретах.
    options.UseNpgsql(connectionString);
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Один email — один аккаунт. Проверку и хеширование пароля выполняет Identity.
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = true;
        // Блокировка защищает аккаунт, ограничитель запросов ниже — адрес клиента.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// При активности билет авторизации продлевается. Без активности он истекает через три часа.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // Локально разрешаем профиль http, вне Development cookie отправляется только по HTTPS.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(3);
});

// Токен в POST-форме не даёт чужому сайту отправить запрос от имени пользователя.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    // Локально разрешаем профиль http, вне Development cookie отправляется только по HTTPS.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync("Слишком много запросов. Повторите попытку позже.", cancellationToken);
    };
    // Для каждого IP свой счётчик: десять запросов за пять минут, без очереди.
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

// Общий фильтр проверяет токен у изменяющих запросов; открытие страницы GET не блокируется.
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    // Таблицы заранее импортируются из Database/shoestore-schema.sql в отдельную локальную БД.
    var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    if (!await roles.RoleExistsAsync("Customer"))
    {
        var result = await roles.CreateAsync(new IdentityRole("Customer"));
        if (!result.Succeeded)
            throw new InvalidOperationException($"Не удалось создать роль Customer: {string.Join("; ", result.Errors.Select(error => error.Description))}");
    }
}

// Вне Development скрываем подробности исключений за общей страницей ошибки.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    // Заголовки добавляем перед отправкой ответа: к этому моменту уже известен пользователь.
    context.Response.OnStarting(() =>
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy"] = "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data: blob:; font-src 'self'; style-src 'self' 'unsafe-inline'; script-src 'self'";
        // Страницы аккаунта не должны оставаться в кеше браузера после выхода.
        if (context.User.Identity?.IsAuthenticated == true || context.Request.Path.StartsWithSegments("/Account"))
            headers.CacheControl = "no-store, max-age=0";
        return Task.CompletedTask;
    });
    await next();
});
app.UseStaticFiles();
// Сначала выбираем маршрут и проверяем лимиты, затем Identity читает cookie.
// Только после этого можно решать, разрешён ли доступ к выбранному действию.
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;
