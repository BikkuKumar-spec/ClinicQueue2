using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using ClinicQueue.Shared.Models;
using ClinicQueue.Shared.DTOs;

namespace ClinicQueue.Services
{
    /// <summary>
    /// Represents one output block from the SLM — either english_output or final_output.
    /// english_output  → always English, used for stage/logic checks
    /// final_output    → user's language, used for display text sent to WhatsApp
    /// </summary>
    public class AiChatResponse
    {
        [JsonPropertyName("reply_message")]
        public string ReplyMessage { get; set; } = string.Empty;

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = string.Empty;

        [JsonPropertyName("extracted_entities")]
        public AiEntities ExtractedEntities { get; set; } = new();

        [JsonPropertyName("missing_info")]
        public List<string> MissingInfo { get; set; } = new();

        // Legacy properties preserved as nullable to avoid breaking if model drifts
        [JsonPropertyName("stage")] public string? Stage { get; set; }
        [JsonPropertyName("question")] public string? Question { get; set; }
    }

    public class AiEntities
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

    /// <summary>
    /// Maps the full SLM response JSON:
    /// {
    ///   "original_language": "hin_Deva",
    ///   "english_input":     "...",
    ///   "english_output":    { reply_message, intent, extracted_entities, missing_info },
    ///   "final_output":      { reply_message, intent, extracted_entities, missing_info }
    /// }
    /// </summary>
    public class ChatResponseWrapper
    {
        /// <summary>Always English — use this for stage checks and business logic.</summary>
        [JsonPropertyName("english_output")]
        public AiChatResponse EnglishOutput { get; set; } = new();

        /// <summary>User's language — use this for text sent back to the user on WhatsApp.</summary>
        [JsonPropertyName("final_output")]
        public AiChatResponse FinalOutput { get; set; } = new();

        /// <summary>The detected language code e.g. "hin_Deva", "eng_Latn".</summary>
        [JsonPropertyName("original_language")]
        public string? OriginalLanguage { get; set; }
    }

    public interface IAISymptomService
    {
        Task<AiChatResponse> ChatAsync(string phoneNumber, string message);

        /// <summary>
        /// Returns the English output (for logic), the final translated output (for display), 
        /// and the language actually detected by the AI.
        /// </summary>
        Task<(AiChatResponse English, AiChatResponse Display, string? DetectedLang)> ChatWithDisplayAsync(string phoneNumber, string message);

        Task ResetSessionAsync(string phoneNumber);
        Task<string> DetectLanguageAsync(string text);
    }
}