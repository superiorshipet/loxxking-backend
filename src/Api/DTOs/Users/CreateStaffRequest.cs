namespace Api.DTOs.Users;

public class CreateStaffRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
}
