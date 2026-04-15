using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace ClinicQueue.Infrastructure.Persistence.Identity;

public class AuthService(
    UserManager<ClinicIdentityUser> userManager,
    ITokenProvider tokenProvider) : IAuthService
{
    public async Task<Result<AuthTokenDto>> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user is null)
        {
            return Result<AuthTokenDto>.Failure("Invalid username or password.");
        }

        var validPassword = await userManager.CheckPasswordAsync(user, password);
        if (!validPassword)
        {
            return Result<AuthTokenDto>.Failure("Invalid username or password.");
        }

        var token = await tokenProvider.CreateTokenAsync(user);
        return Result<AuthTokenDto>.Success(token);
    }

    public async Task<Result<AuthTokenDto>> RegisterAsync(string username, string password, string role, CancellationToken cancellationToken = default)
    {
        var existing = await userManager.FindByNameAsync(username);
        if (existing is not null)
        {
            return Result<AuthTokenDto>.Failure("User already exists.");
        }

        var user = new ClinicIdentityUser
        {
            UserName = username,
            Email = username,
            RoleName = string.IsNullOrWhiteSpace(role) ? "Staff" : role.Trim()
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            var message = string.Join("; ", createResult.Errors.Select(x => x.Description));
            return Result<AuthTokenDto>.Failure(message);
        }

        var token = await tokenProvider.CreateTokenAsync(user);
        return Result<AuthTokenDto>.Success(token);
    }
}
