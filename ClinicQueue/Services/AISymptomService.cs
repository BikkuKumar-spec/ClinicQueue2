using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ClinicQueue.Shared.DTOs;

namespace ClinicQueue.Services
{
    public class AISymptomService : IAISymptomService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AISymptomService> _logger;
        private readonly ITranslationService _translator;

        public AISymptomService(
            HttpClient httpClient,
            ILogger<AISymptomService> logger,
            ITranslationService translator)
        {
            _httpClient = httpClient;
            _logger = logger;
            _translator = translator;
        }

        /// <summary>
        /// Legacy method — returns final_output only (backwards compatible).
        /// </summary>
        public async Task<AiChatResponse> ChatAsync(string phoneNumber, string message)
        {
            var (_, display, _) = await ChatWithDisplayAsync(phoneNumber, message);
            return display;
        }

        /// <summary>
        /// Calls the SLM and returns:
        ///   English  = english_output (stage, specialty etc. always in English for logic checks)
        ///   Display  = final_output   (text already translated to user's language for WhatsApp)
        /// </summary>
        public async Task<(AiChatResponse English, AiChatResponse Display, string? DetectedLang)> ChatWithDisplayAsync(string phoneNumber, string message)
        {
            try
            {
                Console.WriteLine($"[AI SERVICE] Calling /chat for {phoneNumber}, input='{message}'");

                var requestBody = new { text = message, session_id = phoneNumber };
                var response = await _httpClient.PostAsJsonAsync("chat", requestBody);
                response.EnsureSuccessStatusCode();

                var wrapper = await response.Content.ReadFromJsonAsync<ChatResponseWrapper>();

                if (wrapper == null)
                {
                    Console.WriteLine("[AI SERVICE] Null wrapper returned from SLM");
                    var fallback = new AiChatResponse { Stage = "questioning", Question = "Could you please provide more details?" };
                    return (fallback, fallback, "eng_Latn");
                }

                Console.WriteLine($"[AI SERVICE] english_output.stage='{wrapper.EnglishOutput.Stage}'");
                Console.WriteLine($"[AI SERVICE] final_output.stage='{wrapper.FinalOutput.Stage}'");
                Console.WriteLine($"[AI SERVICE] original_language='{wrapper.OriginalLanguage}'");

                return (wrapper.EnglishOutput, wrapper.FinalOutput, wrapper.OriginalLanguage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Python AI Service");
                var fallback = new AiChatResponse { Stage = "questioning", Question = "Service temporarily unavailable. Please try again later." };
                return (fallback, fallback, "eng_Latn");
            }
        }

        public async Task ResetSessionAsync(string phoneNumber)
        {
            try
            {
                await _httpClient.PostAsync($"reset?session_id={phoneNumber}", null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reset AI session");
            }
        }

        public async Task<string> DetectLanguageAsync(string text)
        {
            return await _translator.DetectLanguageAsync(text);
        }
    }
}