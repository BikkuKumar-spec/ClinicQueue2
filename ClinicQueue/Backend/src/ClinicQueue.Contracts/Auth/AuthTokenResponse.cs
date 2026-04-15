namespace ClinicQueue.Contracts.Auth;

public sealed record AuthTokenResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string UserId,
    string Role);
