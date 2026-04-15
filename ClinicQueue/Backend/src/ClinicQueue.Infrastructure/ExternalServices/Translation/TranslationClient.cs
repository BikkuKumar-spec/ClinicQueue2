using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ClinicQueue.Infrastructure.ExternalServices.Translation;

public interface ITranslationClient
{
    Task<string> DetectAsync(string text, CancellationToken cancellationToken = default);
    Task<string> TranslateAsync(string text, string srcLang, string targetLang, CancellationToken cancellationToken = default);
    Task<(string EnglishText, string DetectedLang)> ToEnglishAsync(string text, CancellationToken cancellationToken = default);
    Task<string> FromEnglishAsync(string englishText, string targetLang, CancellationToken cancellationToken = default);
}

public class TranslationClient(HttpClient httpClient, ILogger<TranslationClient> logger) : ITranslationClient
{
    public async Task<string> DetectAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Calling translation service: {Url}", "/detect");
            var response = await httpClient.PostAsJsonAsync("/detect", new { text }, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Translation detect call failed with status {StatusCode}", (int)response.StatusCode);
                return "eng_Latn";
            }

            var payload = await response.Content.ReadFromJsonAsync<DetectResponse>(cancellationToken: cancellationToken);
            return string.IsNullOrWhiteSpace(payload?.Language) ? "eng_Latn" : payload.Language;
        }
        catch
        {
            logger.LogWarning("Translation detect call failed due to exception.");
            return "eng_Latn";
        }
    }

    public async Task<string> TranslateAsync(string text, string srcLang, string targetLang, CancellationToken cancellationToken = default)
    {
        if (string.Equals(srcLang, targetLang, StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        try
        {
            logger.LogInformation("Calling translation service: {Url}", "/translate");
            var response = await httpClient.PostAsJsonAsync(
                "/translate",
                new { text, src_lang = srcLang, target_lang = targetLang },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Translation call failed with status {StatusCode}", (int)response.StatusCode);
                return text;
            }

            var payload = await response.Content.ReadFromJsonAsync<TranslateResponse>(cancellationToken: cancellationToken);
            return string.IsNullOrWhiteSpace(payload?.TranslatedText) ? text : payload.TranslatedText;
        }
        catch
        {
            logger.LogWarning("Translation call failed due to exception.");
            return text;
        }
    }

    public async Task<(string EnglishText, string DetectedLang)> ToEnglishAsync(string text, CancellationToken cancellationToken = default)
    {
        var detected = await DetectAsync(text, cancellationToken);
        if (string.Equals(detected, "eng_Latn", StringComparison.OrdinalIgnoreCase))
        {
            return (text, detected);
        }

        var translated = await TranslateAsync(text, detected, "eng_Latn", cancellationToken);
        return (translated, detected);
    }

    public async Task<string> FromEnglishAsync(string englishText, string targetLang, CancellationToken cancellationToken = default)
    {
        if (string.Equals(targetLang, "eng_Latn", StringComparison.OrdinalIgnoreCase))
        {
            return englishText;
        }

        return await TranslateAsync(englishText, "eng_Latn", targetLang, cancellationToken);
    }

    private sealed class DetectResponse
    {
        [JsonPropertyName("language")]
        public string? Language { get; init; }
    }

    private sealed class TranslateResponse
    {
        [JsonPropertyName("translated_text")]
        public string? TranslatedText { get; init; }
    }
}
