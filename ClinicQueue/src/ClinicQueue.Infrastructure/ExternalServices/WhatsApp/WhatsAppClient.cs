using System.Net.Http.Json;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.Interfaces;

namespace ClinicQueue.Infrastructure.ExternalServices.WhatsApp;

public class WhatsAppClient(HttpClient httpClient) : IWhatsAppGateway
{
    public async Task<Result<bool>> SendTextAsync(string to, string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return Result<bool>.Failure("Failed to send WhatsApp message. Message body is empty.");
        }

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to,
            type = "text",
            text = new { body = message.Trim() }
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
