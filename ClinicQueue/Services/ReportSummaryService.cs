using System.Text;
using System.Text.Json;

namespace ClinicQueue.Services
{
    public interface IReportSummaryService
    {
        Task<string> GenerateReportSummaryAsync(string reportText);
    }

    /// <summary>
    /// Calls the local Ollama endpoint (Qwen 2.5) to summarize medical report text.
    /// Separated from the chatbot AI pipeline to avoid polluting conversation history.
    /// </summary>
    public class ReportSummaryService : IReportSummaryService
    {
        private readonly HttpClient _httpClient;
        private readonly string _ollamaEndpoint;
        private readonly ILogger<ReportSummaryService> _logger;

        private const string SystemPrompt =
            "You are an expert medical assistant. The user has provided raw text from a medical lab report. " +
            "Summarize the key findings in simple, easy-to-understand language. " +
            "Highlight any abnormal values (e.g., high sugar, low hemoglobin) and explain what they generally mean. " +
            "Keep it under 150 words. Do NOT prescribe medication.";

        public ReportSummaryService(
            HttpClient httpClient,
            string ollamaEndpoint,
            ILogger<ReportSummaryService> logger)
        {
            _httpClient = httpClient;
            _ollamaEndpoint = ollamaEndpoint;
            _logger = logger;
        }

        public async Task<string> GenerateReportSummaryAsync(string reportText)
        {
            // Truncate extremely long reports to prevent token overflow
            const int maxChars = 4000;
            if (reportText.Length > maxChars)
                reportText = reportText[..maxChars] + "\n[... report truncated for processing ...]";

            var requestBody = new
            {
                model = "llama3.1:latest",
                prompt = $"Here is the raw text extracted from a medical lab report:\n\n{reportText}\n\nPlease summarize this report.",
                system = SystemPrompt,
                stream = false,
                options = new { temperature = 0.3 }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling Ollama for report summarization ({CharCount} chars)", reportText.Length);

            var response = await _httpClient.PostAsync(_ollamaEndpoint, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var summary = doc.RootElement.GetProperty("response").GetString() ?? "";

            _logger.LogInformation("Ollama report summary generated ({SummaryLength} chars)", summary.Length);
            return summary.Trim();
        }
    }
}
