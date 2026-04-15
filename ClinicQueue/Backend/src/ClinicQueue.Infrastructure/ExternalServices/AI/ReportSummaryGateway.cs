using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.Interfaces;

namespace ClinicQueue.Infrastructure.ExternalServices.AI;

public class ReportSummaryGateway(HttpClient httpClient) : IReportSummaryGateway
{
    public async Task<Result<string>> GenerateSummaryAsync(string extractedText, CancellationToken cancellationToken = default)
    {
        try
        {
            // Translation service currently exposes /chat, not /summary.
            // Use a dedicated session id to avoid polluting user booking conversations.
            var prompt =
                "[REPORT_SUMMARY]\n" +
                "Summarize this uploaded medical report for a patient. " +
                "Return 4 to 6 short bullet points with key findings and any abnormal values if present. " +
                "If the text is unreadable or non-medical, say that clearly in one short sentence.\n\n" +
                extractedText;

            var requestBody = new
            {
                text = prompt,
                session_id = $"report-summary-{Guid.NewGuid():N}"
            };

            var response = await httpClient.PostAsJsonAsync("/chat", requestBody, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<string>.Failure($"Summary service returned {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<ChatSummaryResponse>(cancellationToken: cancellationToken);

            var reply = payload?.FinalOutput?.ReplyMessage
                ?? payload?.EnglishOutput?.ReplyMessage
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(reply))
            {
                return Result<string>.Failure("Summary service returned empty summary.");
            }

            if (IsLowQualityReply(reply))
            {
                return Result<string>.Failure("Summary service returned a low-quality reply.");
            }

            return Result<string>.Success(reply.Trim());
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Summary request failed: {ex.Message}");
        }
    }

    private static bool IsLowQualityReply(string reply)
    {
        var lowered = reply.ToLowerInvariant();
        var trimmed = reply.Trim();

        var hasBulletLikeContent = trimmed.Contains("\n-")
            || trimmed.Contains("\n•")
            || trimmed.Contains("\n*")
            || trimmed.Contains("; ");

        return lowered.Contains("i apologize")
            || lowered.Contains("please rephrase")
            || lowered.Contains("having trouble processing")
            || lowered.Contains("service temporarily unavailable")
            || lowered.Contains("here's a summary of your lab report")
            || lowered.Contains("here is a summary of your lab report")
            || lowered.Contains("summary of your lab report")
            || lowered.Contains("here are the key points")
            || lowered.Contains("key points:")
            || lowered.Contains("ai-generated summary")
            || lowered.Contains("verify with your doctor")
            || (trimmed.EndsWith(':') && !hasBulletLikeContent)
            || (lowered.Contains("summary") && !hasBulletLikeContent)
            || lowered.Length < 40;
    }

    private sealed class ChatSummaryResponse
    {
        [JsonPropertyName("english_output")]
        public ChatOutput? EnglishOutput { get; init; }

        [JsonPropertyName("final_output")]
        public ChatOutput? FinalOutput { get; init; }
    }

    private sealed class ChatOutput
    {
        [JsonPropertyName("reply_message")]
        public string? ReplyMessage { get; init; }
    }
}
