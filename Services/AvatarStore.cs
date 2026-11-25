using System.Data.Common;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShoeStore.Data;
using SkiaSharp;

namespace ShoeStore.Services;

public sealed class AvatarStore(ApplicationDbContext db, IHttpContextAccessor http, ILogger<AvatarStore> logger)
{
    public const int MaxPhotoBytes = 5 * 1024 * 1024;
    public const int MaxRequestBytes = 6 * 1024 * 1024;
    public const string ContentType = "image/webp";
    private const int AvatarSize = 256;
    private const int MaxPhotoSide = 4096;
    private Task<bool>? hasAvatar;

    public Task<bool> HasCurrentAvatarAsync(CancellationToken cancellationToken)
    {
        var userId = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return userId is null ? Task.FromResult(false) : hasAvatar ??= HasAvatarAsync(userId, cancellationToken);
    }

    private async Task<bool> HasAvatarAsync(string userId, CancellationToken cancellationToken)
    {
        try
        {
            return await db.UserAvatars.AnyAsync(avatar => avatar.UserId == userId, cancellationToken);
        }
        catch (DbException failure)
        {
            logger.LogWarning("Не удалось проверить фото профиля. Тип ошибки {ErrorType}.", failure.GetType().Name);
            return false;
        }
    }

    public Task<byte[]?> ReadAsync(string userId, CancellationToken cancellationToken) => db.UserAvatars
        .Where(avatar => avatar.UserId == userId)
        .Select(avatar => avatar.Photo)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<string?> SaveAsync(string userId, IFormFile? photo, CancellationToken cancellationToken)
    {
        if (photo is null || photo.Length == 0) return "Выберите фото для загрузки.";
        if (photo.Length > MaxPhotoBytes) return "Размер файла должен быть не больше 5 МБ.";

        await using var input = photo.OpenReadStream();
        using var codec = SKCodec.Create(input);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
            return "Выберите изображение JPG, PNG или WebP.";

        var (width, height) = (codec.Info.Width, codec.Info.Height);
        if (width is <= 0 or > MaxPhotoSide || height is <= 0 or > MaxPhotoSide)
            return "Размер изображения должен быть не больше 4096 × 4096 пикселей.";

        using var bitmap = new SKBitmap(width, height);
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            return "Не удалось прочитать фото. Выберите другой файл.";

        using var thumbnail = SKSurface.Create(new SKImageInfo(AvatarSize, AvatarSize));
        var canvas = thumbnail.Canvas;
        canvas.Clear(SKColors.Transparent);
        Orient(canvas, codec.EncodedOrigin);
        var side = Math.Min(width, height);
        var source = SKRect.Create((width - side) / 2f, (height - side) / 2f, side, side);
        var target = SKRect.Create(AvatarSize, AvatarSize);
        canvas.DrawBitmap(bitmap, source, target, new SKSamplingOptions(SKFilterMode.Linear));
        using var image = thumbnail.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Webp, 85)
            ?? throw new IOException("Не удалось подготовить фото профиля.");

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserAvatars" ("UserId", "Photo") VALUES ({userId}, {data.ToArray()})
            ON CONFLICT ("UserId") DO UPDATE SET "Photo" = EXCLUDED."Photo"
            """, cancellationToken);
        return null;
    }

    private static void Orient(SKCanvas canvas, SKEncodedOrigin origin)
    {
        const float center = AvatarSize / 2f;
        var degrees = origin switch
        {
            SKEncodedOrigin.BottomRight => 180,
            SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom => 90,
            SKEncodedOrigin.LeftBottom => 270,
            _ => 0
        };
        if (degrees != 0) canvas.RotateDegrees(degrees, center, center);
        if (origin is SKEncodedOrigin.TopRight or SKEncodedOrigin.RightBottom) canvas.Scale(-1, 1, center, center);
        if (origin is SKEncodedOrigin.BottomLeft or SKEncodedOrigin.LeftTop) canvas.Scale(1, -1, center, center);
    }
}
