namespace ClinicQueue.Contracts.Auth;

public sealed record RegisterRequest(
    string Username,
    string Password,
    string Role);
