namespace ClinicQueue.Contracts.WhatsApp;

public sealed record SendMessageRequest(
    string To,
    string Message);
