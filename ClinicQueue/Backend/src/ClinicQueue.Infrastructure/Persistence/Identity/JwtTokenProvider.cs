using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClinicQueue.Application.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ClinicQueue.Infrastructure.Persistence.Identity;

public class JwtTokenProvider(IConfiguration configuration) : ITokenProvider
{
    public Task<AuthTokenDto> CreateTokenAsync(ClinicIdentityUser user)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "ClinicQueueSystem";
        var audience = configuration["Jwt:Audience"] ?? "ClinicDashboard";
        var secret = configuration["Jwt:Secret"] ?? "YourSuperSecretKey32CharactersLong!";
        var expiresMinutes = int.TryParse(configuration["Jwt:ExpiresMinutes"], out var value) ? value : 120;

        var expiresAt = DateTime.UtcNow.AddMinutes(expiresMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName ?? string.Empty),
            new(ClaimTypes.Role, user.RoleName)
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: creds);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        var dto = new AuthTokenDto(
            tokenString,
            expiresAt,
            user.Id,
            user.RoleName);

        return Task.FromResult(dto);
    }
}
