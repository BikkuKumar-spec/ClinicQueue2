using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace ClinicQueue.Infrastructure.ExternalServices;

public interface IOcrService
{
    Task<OcrResult> ExtractTextFromImageAsync(byte[] imageBytes, string fileName, CancellationToken cancellationToken = default);
    Task<bool> IsServiceHealthyAsync(CancellationToken cancellationToken = default);
    Task<List<string>> GetSupportedFormatsAsync(CancellationToken cancellationToken = default);
}

public class OcrService(HttpClient httpClient) : IOcrService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif"
    };

    public async Task<OcrResult> ExtractTextFromImageAsync(byte[] imageBytes, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!SupportedExtensions.Contains(extension))
        {
            return OcrResult.FailureResult($"Unsupported image format '{extension}'. Supported: jpg, jpeg, png, bmp, tiff", "UNSUPPORTED_FORMAT");
        }

        try
        {
            using var formContent = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(GetContentType(extension));
            formContent.Add(fileContent, "file", fileName);

            var response = await httpClient.PostAsync("api/v1/extract-text", formContent, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = System.Text.Json.JsonSerializer.Deserialize<OcrApiResponse>(responseBody);
                return OcrResult.FailureResult(error?.Message ?? $"OCR service error: {response.StatusCode}", error?.Error ?? "SERVICE_ERROR");
            }

            var payload = System.Text.Json.JsonSerializer.Deserialize<OcrApiResponse>(responseBody);
            if (payload is null)
            {
                return OcrResult.FailureResult("Failed to parse OCR service response", "PARSE_ERROR");
            }

            return payload.Success
                ? OcrResult.SuccessResult(payload.ExtractedText ?? string.Empty, payload.Confidence, payload.WordCount)
                : OcrResult.FailureResult(payload.Message ?? "OCR extraction failed", payload.Error ?? "OCR_FAILED");
        }
        catch (Exception ex)
        {
            return OcrResult.FailureResult($"Unexpected error: {ex.Message}", "UNEXPECTED_ERROR");
        }
    }

    public async Task<bool> IsServiceHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetSupportedFormatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("api/v1/supported-formats", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return SupportedExtensions.ToList();
            }

            var payload = await response.Content.ReadFromJsonAsync<SupportedFormatsResponse>(cancellationToken: cancellationToken);
            return payload?.SupportedFormats ?? SupportedExtensions.ToList();
        }
        catch
        {
            return SupportedExtensions.ToList();
        }
    }

    private static string GetContentType(string extension)
    {
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            _ => "application/octet-stream"
        };
    }

    private sealed class OcrApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("extracted_text")]
        public string? ExtractedText { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }

        [JsonPropertyName("word_count")]
        public int? WordCount { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    private sealed class SupportedFormatsResponse
    {
        [JsonPropertyName("supported_formats")]
        public List<string>? SupportedFormats { get; set; }
    }
}

public class OcrResult
{
    public bool Success { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public double? Confidence { get; set; }
    public int? WordCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }

    public static OcrResult SuccessResult(string text, double? confidence = null, int? wordCount = null)
    {
        return new OcrResult { Success = true, ExtractedText = text, Confidence = confidence, WordCount = wordCount };
    }

    public static OcrResult FailureResult(string errorMessage, string errorCode = "OCR_FAILED")
    {
        return new OcrResult { Success = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
    }
}
