namespace ClinicQueue.Contracts.WhatsApp;

public sealed record WebhookReplyResponse(
    bool Accepted,
    string? MessageId,
    string? Error);
