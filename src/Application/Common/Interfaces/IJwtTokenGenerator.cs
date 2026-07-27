namespace Application.Common.Interfaces;
using Domain.Entities;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
