using System.Text.Json.Serialization;

namespace ClinicQueue.Contracts.WhatsApp;

public sealed record WebhookEventRequest(
    [property: JsonPropertyName("object")] string? Object,
    [property: JsonPropertyName("entry")] IReadOnlyList<WebhookEntry>? Entry);

public sealed record WebhookEntry(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("changes")] IReadOnlyList<WebhookChange>? Changes);

public sealed record WebhookChange(
    [property: JsonPropertyName("field")] string? Field,
    [property: JsonPropertyName("value")] WebhookValue? Value);

public sealed record WebhookValue(
    [property: JsonPropertyName("messaging_product")] string? MessagingProduct,
    [property: JsonPropertyName("messages")] IReadOnlyList<WebhookMessage>? Messages,
    [property: JsonPropertyName("statuses")] IReadOnlyList<WebhookStatus>? Statuses);

public sealed record WebhookMessage(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("from")] string? From,
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("text")] WebhookText? Text,
    [property: JsonPropertyName("interactive")] WebhookInteractive? Interactive,
    [property: JsonPropertyName("document")] WebhookDocument? Document,
    [property: JsonPropertyName("image")] WebhookImage? Image);

public sealed record WebhookText(
    [property: JsonPropertyName("body")] string? Body);

public sealed record WebhookInteractive(
    [property: JsonPropertyName("type")] string? Type,
    [property: JsonPropertyName("button_reply")] WebhookInteractiveReply? ButtonReply,
    [property: JsonPropertyName("list_reply")] WebhookInteractiveReply? ListReply);

public sealed record WebhookInteractiveReply(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("title")] string? Title);

public sealed record WebhookDocument(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("mime_type")] string? MimeType,
    [property: JsonPropertyName("filename")] string? Filename);

public sealed record WebhookImage(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("mime_type")] string? MimeType,
    [property: JsonPropertyName("sha256")] string? Sha256);

public sealed record WebhookStatus(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("recipient_id")] string? RecipientId);
