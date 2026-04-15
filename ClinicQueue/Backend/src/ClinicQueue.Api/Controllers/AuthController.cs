using ClinicQueue.Api.Common;
using ClinicQueue.Api.Mappings;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthTokenResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Username, request.Password, cancellationToken);
        return this.ToActionResult(Map(result, value => value.ToResponse()));
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthTokenResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request.Username, request.Password, request.Role, cancellationToken);
        return this.ToActionResult(Map(result, value => value.ToResponse()));
    }

    private static Result<TOutput> Map<TInput, TOutput>(Result<TInput> result, Func<TInput, TOutput> mapper)
    {
        if (!result.IsSuccess || result.Value is null)
        {
            return Result<TOutput>.Failure(result.Error ?? "Request failed.");
        }

        return Result<TOutput>.Success(mapper(result.Value));
    }
}
