using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClinicQueue.Services
{
    public class TranslationService : ITranslationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<TranslationService> _logger;

        // ✅ Use UnsafeRelaxedJsonEscaping so Hindi/Marathi characters are NOT
        // escaped to \uXXXX when serializing POST bodies — microservice needs real Unicode
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true
        };

        public TranslationService(HttpClient httpClient, IConfiguration configuration, ILogger<TranslationService> logger)
        {
            _httpClient = httpClient;
            _baseUrl = configuration["AI:TranslationServiceUrl"] ?? "http://localhost:5001";
            _logger = logger;
        }

        public async Task<string> DetectLanguageAsync(string text)
        {
            try
            {
                var content = JsonContent.Create(new { text }, options: _jsonOptions);
                var response = await _httpClient.PostAsync($"{_baseUrl}/detect", content);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<DetectResponse>(_jsonOptions);
                _logger.LogInformation("[DETECT] '{text}' → '{lang}'", text, result?.Language);
                return result?.Language ?? "eng_Latn";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Language detection failed");
                return "eng_Latn";
            }
        }

        public async Task<string> TranslateAsync(string text, string srcLang, string targetLang)
        {
            if (srcLang == targetLang) return text;

            try
            {
                // ✅ Use JsonContent.Create with UnsafeRelaxedJsonEscaping so Hindi text
                // is sent as real Unicode characters, not escaped \uXXXX sequences
                var content = JsonContent.Create(new
                {
                    text,
                    src_lang = srcLang,
                    target_lang = targetLang
                }, options: _jsonOptions);

                var response = await _httpClient.PostAsync($"{_baseUrl}/translate", content);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<TranslateResponse>(_jsonOptions);
                _logger.LogInformation("[TRANSLATE] {src}→{tgt} | in='{text}' | out='{translated}'",
                    srcLang, targetLang, text, result?.TranslatedText);

                return result?.TranslatedText ?? text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Translation failed from {Src} to {Target}", srcLang, targetLang);
                return text;
            }
        }

        public async Task<(string EnglishText, string DetectedLang)> ToEnglishAsync(string text)
        {
            var detected = await DetectLanguageAsync(text);
            if (detected == "eng_Latn") return (text, detected);

            var translated = await TranslateAsync(text, detected, "eng_Latn");
            return (translated, detected);
        }

        public async Task<string> FromEnglishAsync(string englishText, string targetLang)
        {
            if (targetLang == "eng_Latn") return englishText;
            return await TranslateAsync(englishText, "eng_Latn", targetLang);
        }

        private class DetectResponse
        {
            public string? Language { get; set; }
        }

        private class TranslateResponse
        {
            [System.Text.Json.Serialization.JsonPropertyName("translated_text")]
            public string? TranslatedText { get; set; }
        }
    }
}