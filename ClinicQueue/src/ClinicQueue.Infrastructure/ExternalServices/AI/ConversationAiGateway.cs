using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ClinicQueue.Infrastructure.ExternalServices.AI;

public interface IConversationAiGateway
{
    Task<(AiChatResponse English, AiChatResponse Display, string? DetectedLang)> ChatWithDisplayAsync(string sessionId, string message, CancellationToken cancellationToken = default);
    Task ResetSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

public sealed class ConversationAiGateway(HttpClient httpClient, ILogger<ConversationAiGateway> logger) : IConversationAiGateway
{
    public async Task<(AiChatResponse English, AiChatResponse Display, string? DetectedLang)> ChatWithDisplayAsync(string sessionId, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = new { text = message, session_id = sessionId };
            var response = await httpClient.PostAsJsonAsync("chat", requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var wrapper = await response.Content.ReadFromJsonAsync<ChatResponseWrapper>(cancellationToken: cancellationToken);
            if (wrapper is null)
            {
                var fallback = new AiChatResponse
                {
                    ReplyMessage = "I'm sorry, I didn't quite understand that. Could you please rephrase?",
                    Intent = "Other",
                    ExtractedEntities = new AiEntities()
                };
                return (fallback, fallback, "eng_Latn");
            }

            return (wrapper.EnglishOutput ?? new AiChatResponse(), wrapper.FinalOutput ?? new AiChatResponse(), wrapper.OriginalLanguage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Conversation AI request failed");
            var fallback = new AiChatResponse
            {
                ReplyMessage = "Service temporarily unavailable. Please try again later.",
                Intent = "Other",
                ExtractedEntities = new AiEntities()
            };
            return (fallback, fallback, "eng_Latn");
        }
    }

    public async Task ResetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await httpClient.PostAsync($"reset?session_id={sessionId}", null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to reset AI conversation session {SessionId}", sessionId);
        }
    }
}

public sealed class ChatResponseWrapper
{
    [JsonPropertyName("english_output")]
    public AiChatResponse? EnglishOutput { get; set; }

    [JsonPropertyName("final_output")]
    public AiChatResponse? FinalOutput { get; set; }

    [JsonPropertyName("original_language")]
    public string? OriginalLanguage { get; set; }
}

public sealed class AiChatResponse
{
    [JsonPropertyName("reply_message")]
    public string ReplyMessage { get; set; } = string.Empty;

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = string.Empty;

    [JsonPropertyName("extracted_entities")]
    public AiEntities ExtractedEntities { get; set; } = new();

    [JsonPropertyName("missing_info")]
    public List<string> MissingInfo { get; set; } = new();

    [JsonPropertyName("stage")]
    public string? Stage { get; set; }

    [JsonPropertyName("question")]
    public string? Question { get; set; }
}

public sealed class AiEntities
{
    [JsonPropertyName("specialty_needed")]
    public string? SpecialtyNeeded { get; set; }

    [JsonPropertyName("preferred_doctor")]
    public string? PreferredDoctor { get; set; }

    [JsonPropertyName("preferred_date")]
    public string? PreferredDate { get; set; }

    [JsonPropertyName("preferred_time")]
    public string? PreferredTime { get; set; }

    [JsonPropertyName("patient_name")]
    public string? PatientName { get; set; }
}
