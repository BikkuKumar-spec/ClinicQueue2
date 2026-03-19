using ClinicQueue.Application.DTOs;

namespace ClinicQueue.Infrastructure.Persistence.Identity;

public interface ITokenProvider
{
    Task<AuthTokenDto> CreateTokenAsync(ClinicIdentityUser user);
}
