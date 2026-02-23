using System.Threading.Tasks;

namespace ClinicQueue.Services
{
    public interface ITranslationService
    {
        /// <summary>
        /// Detects the language of the source text.
        /// Returns a language code like "hin_Deva", "mar_Deva", or "eng_Latn".
        /// </summary>
        Task<string> DetectLanguageAsync(string text);

        /// <summary>
        /// Translates text from source language to target language.
        /// </summary>
        Task<string> TranslateAsync(string text, string srcLang, string targetLang);

        /// <summary>
        /// Helper to translate user input to English for AI processing.
        /// </summary>
        Task<(string EnglishText, string DetectedLang)> ToEnglishAsync(string text);

        /// <summary>
        /// Helper to translate AI response back to user's language.
        /// </summary>
        Task<string> FromEnglishAsync(string englishText, string targetLang);
    }
}
