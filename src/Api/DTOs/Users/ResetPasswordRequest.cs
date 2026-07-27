namespace Api.DTOs.Users;

public class ResetPasswordRequest
{
    public Guid UserId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
}
