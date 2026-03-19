using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        private readonly string _ollamaModel;
        private readonly bool _useOllama;
        private readonly ILogger<ReportSummaryService> _logger;

        private const string SystemPrompt =
            "You are a friendly medical assistant. " +
            "Create a short, human-style summary of a lab report in plain language. " +
            "Use 3-5 short bullet points, mention abnormal values first, and keep it under 90 words. " +
            "Do not prescribe medicines.";

        public ReportSummaryService(
            HttpClient httpClient,
            string ollamaEndpoint,
            string ollamaModel,
            bool useOllama,
            ILogger<ReportSummaryService> logger)
        {
            _httpClient = httpClient;
            _ollamaEndpoint = ollamaEndpoint;
            _ollamaModel = ollamaModel;
            _useOllama = useOllama;
            _logger = logger;

            _logger.LogInformation(
                "Report summary mode initialized. UseOllama={UseOllama}, Model={Model}, Endpoint={Endpoint}",
                _useOllama,
                _ollamaModel,
                _ollamaEndpoint);
        }

        public async Task<string> GenerateReportSummaryAsync(string reportText)
        {
            if (string.IsNullOrWhiteSpace(reportText))
                return "I could not read enough text from the report to summarize it.";

            // Truncate extremely long reports to prevent token overflow
            const int maxChars = 4000;
            if (reportText.Length > maxChars)
                reportText = reportText[..maxChars] + "\n[... report truncated for processing ...]";

            if (!_useOllama)
            {
                _logger.LogInformation("Using local fallback summary mode (Ollama disabled)");
                return BuildFallbackSummary(reportText);
            }

            var requestBody = new
            {
                model = _ollamaModel,
                prompt = $"Here is the raw text extracted from a medical lab report:\n\n{reportText}\n\nPlease summarize this report.",
                system = SystemPrompt,
                stream = false,
                options = new { temperature = 0.3 }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation("Calling Ollama for report summarization ({CharCount} chars)", reportText.Length);

            try
            {
                var response = await _httpClient.PostAsync(_ollamaEndpoint, content);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama returned non-success status code: {StatusCode}", response.StatusCode);
                    return BuildFallbackSummary(reportText);
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseJson);

                if (!doc.RootElement.TryGetProperty("response", out var responseProperty))
                {
                    _logger.LogWarning("Ollama response did not contain 'response' property");
                    return BuildFallbackSummary(reportText);
                }

                var summary = responseProperty.GetString() ?? "";
                if (string.IsNullOrWhiteSpace(summary))
                {
                    _logger.LogWarning("Ollama returned an empty summary");
                    return BuildFallbackSummary(reportText);
                }

                _logger.LogInformation("Ollama report summary generated ({SummaryLength} chars)", summary.Length);
                return summary.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ollama summary call failed, using fallback summary");
                return BuildFallbackSummary(reportText);
            }
        }

        private static string BuildFallbackSummary(string reportText)
        {
            // Lightweight local summary used when AI service is unavailable.
            var normalized = reportText.Replace("\r", "");
            var lines = normalized
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(l => l.Length > 2)
                .ToList();

            if (lines.Count == 0)
                return "I could not read enough report text. Please upload a clearer PDF or image.";

            var flaggedKeywords = new[]
            {
                "high", "low", "abnormal", "critical", "positive", "negative",
                "elevated", "decreased", "borderline", "deficient"
            };

            var flagged = lines
                .Where(l => flaggedKeywords.Any(k => l.Contains(k, StringComparison.OrdinalIgnoreCase)))
                .Take(5)
                .ToList();

            var numericFindings = lines
                .Where(l => Regex.IsMatch(l, @"\d"))
                .Take(5)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("Here is a quick summary:");

            if (flagged.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Possible important findings:");
                foreach (var f in flagged)
                    sb.AppendLine($"- {f}");
            }

            if (numericFindings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Key values seen:");
                foreach (var v in numericFindings)
                    sb.AppendLine($"- {v}");
            }

            if (flagged.Count == 0 && numericFindings.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("I could read parts of the report, but no clear abnormal markers were detected.");
            }

            var result = sb.ToString().Trim();
            // Keep message size safe for WhatsApp text payloads.
            return result.Length > 1400 ? result[..1400] + "..." : result;
        }
    }
}
