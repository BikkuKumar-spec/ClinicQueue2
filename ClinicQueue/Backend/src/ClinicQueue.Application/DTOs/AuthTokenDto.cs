namespace ClinicQueue.Application.DTOs;

public sealed record AuthTokenDto(
    string AccessToken,
    DateTime ExpiresAt,
    string UserId,
    string Role);
