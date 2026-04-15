using System.Collections.Concurrent;
using ClinicQueue.Api.Common;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Contracts.WhatsApp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WhatsAppController(
    IWhatsAppGateway whatsAppGateway,
    IWhatsAppBotProcessor whatsAppBotProcessor,
    IConfiguration configuration,
    ILogger<WhatsAppController> logger) : ControllerBase
{
    private static readonly ConcurrentDictionary<string, DateTimeOffset> ProcessedMessageIds = new(StringComparer.Ordinal);
    private static readonly TimeSpan MessageIdTtl = TimeSpan.FromMinutes(15);

    private readonly string _verifyToken = configuration["Meta:WhatsApp:WebhookVerifyToken"] ?? "VERIFY_TOKEN";

    [HttpGet("webhook")]
    [AllowAnonymous]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase)
            && string.Equals(token, _verifyToken, StringComparison.Ordinal))
        {
            return Ok(challenge);
        }

        return Forbid();
    }

    [HttpPost("send")]
    [Authorize]
    [ProducesResponseType(typeof(WebhookReplyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<WebhookReplyResponse>> SendMessage([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await whatsAppGateway.SendTextAsync(request.To, request.Message, cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ToActionResult(ClinicQueue.Application.Common.Result<WebhookReplyResponse>.Failure(result.Error ?? "Message send failed."));
        }

        return Ok(new WebhookReplyResponse(true, null, null));
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WebhookReplyResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<WebhookReplyResponse>> ReceiveWebhook([FromBody] WebhookEventRequest request, CancellationToken cancellationToken)
    {
        var messages = request.Entry?
            .SelectMany(entry => entry.Changes ?? [])
            .Select(change => change.Value)
            .Where(value => value is not null)
            .SelectMany(value => value!.Messages ?? [])
            .ToList() ?? [];

        var firstMessage = messages.FirstOrDefault();

        logger.LogInformation(
            "Webhook event received. Type: {Type}, From: {From}, MessageType: {MessageType}",
            request.Object ?? "unknown",
            firstMessage?.From,
            firstMessage?.Type);

        foreach (var message in messages)
        {
            logger.LogInformation("Message type received: {Type}", message.Type ?? "unknown");

            if (string.IsNullOrWhiteSpace(message.From))
            {
                continue;
            }

            if (!TryBeginMessageProcessing(message.Id))
            {
                logger.LogInformation("Skipping duplicate WhatsApp message id {MessageId}", message.Id);
                continue;
            }

            var input = ExtractUserInput(message);
            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            try
            {
                // WhatsApp retries webhooks quickly. Keep processing independent of request-abort cancellation.
                await whatsAppBotProcessor.ProcessMessageAsync(message.From, input, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed processing WhatsApp message id {MessageId} from {From}", message.Id, message.From);
                MarkMessageAsUnprocessed(message.Id);
            }
        }

        return Ok(new WebhookReplyResponse(true, null, null));
    }

    private static bool TryBeginMessageProcessing(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return true;
        }

        CleanupProcessedMessageIds(DateTimeOffset.UtcNow);
        return ProcessedMessageIds.TryAdd(messageId, DateTimeOffset.UtcNow);
    }

    private static void MarkMessageAsUnprocessed(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return;
        }

        ProcessedMessageIds.TryRemove(messageId, out _);
    }

    private static void CleanupProcessedMessageIds(DateTimeOffset now)
    {
        foreach (var pair in ProcessedMessageIds)
        {
            if (now - pair.Value > MessageIdTtl)
            {
                ProcessedMessageIds.TryRemove(pair.Key, out _);
            }
        }
    }

    private static string ExtractUserInput(WebhookMessage message)
    {
        return message.Type switch
        {
            "text" => message.Text?.Body?.Trim() ?? string.Empty,
            "interactive" when string.Equals(message.Interactive?.Type, "button_reply", StringComparison.OrdinalIgnoreCase)
                => message.Interactive?.ButtonReply?.Id ?? string.Empty,
            "interactive" when string.Equals(message.Interactive?.Type, "list_reply", StringComparison.OrdinalIgnoreCase)
                => message.Interactive?.ListReply?.Id ?? string.Empty,
            "document" when string.Equals(message.Document?.MimeType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                => $"PDF_UPLOAD:{message.Document?.Id}",
            "document" => "DOC_UNSUPPORTED",
            "image" when !string.IsNullOrWhiteSpace(message.Image?.Id)
                => $"IMAGE_UPLOAD:{message.Image?.Id}",
            _ => string.Empty
        };
    }
}
