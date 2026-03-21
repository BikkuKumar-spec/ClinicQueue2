using System.Net.Http.Json;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.Interfaces;

namespace ClinicQueue.Infrastructure.ExternalServices.WhatsApp;

public class WhatsAppClient(HttpClient httpClient) : IWhatsAppGateway
{
    private const int MaxBodyLength = 3500;

    public async Task<Result<bool>> SendTextAsync(string to, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Result<bool>.Failure("Failed to send WhatsApp message. Message body is empty.");
        }

        var safeMessage = message.Trim();
        if (safeMessage.Length > MaxBodyLength)
        {
            safeMessage = safeMessage[..MaxBodyLength].TrimEnd() + "...";
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to,
            type = "text",
            text = new { body = safeMessage }
        };

        var response = await httpClient.PostAsJsonAsync("messages", payload, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return Result<bool>.Success(true);
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var error = string.IsNullOrWhiteSpace(content)
            ? "Failed to send WhatsApp message."
            : $"Failed to send WhatsApp message. Response: {content}";

        return Result<bool>.Failure(error);
    }
}
