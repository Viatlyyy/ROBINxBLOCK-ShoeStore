namespace ShoeStore.Models;

public sealed class UserAvatar
{
    public required string UserId { get; init; }
    public required byte[] Photo { get; set; }
}
