namespace Api.DTOs.Users;

public class AdminChangePasswordRequest
{
    public Guid UserId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}
