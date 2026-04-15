using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;

namespace ClinicQueue.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthTokenDto>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task<Result<AuthTokenDto>> RegisterAsync(string username, string password, string role, CancellationToken cancellationToken = default);
}
