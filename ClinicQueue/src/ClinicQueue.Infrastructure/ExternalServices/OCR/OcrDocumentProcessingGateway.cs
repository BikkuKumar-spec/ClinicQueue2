using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ClinicQueue.Infrastructure.ExternalServices.OCR;

public class OcrDocumentProcessingGateway(HttpClient httpClient, ILogger<OcrDocumentProcessingGateway> logger) : IDocumentProcessingGateway
{
    public async Task<Result<MedicalDocumentExtractionDto>> ExtractAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(fileBytes);
            bytes.Headers.ContentType = MediaTypeHeaderValue.Parse(GetContentType(fileName));
            content.Add(bytes, "file", fileName);

            logger.LogInformation("Calling OCR service: {Url}", "api/v1/extract-text");
            var response = await httpClient.PostAsync("api/v1/extract-text", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var errorPayload = await response.Content.ReadFromJsonAsync<OcrResponse>(cancellationToken: cancellationToken);
                var errorMessage = !string.IsNullOrWhiteSpace(errorPayload?.Message)
                    ? errorPayload.Message
                    : $"OCR service returned {(int)response.StatusCode}.";

                logger.LogWarning("OCR service returned status {StatusCode}: {Message}", (int)response.StatusCode, errorMessage);
                return Result<MedicalDocumentExtractionDto>.Failure(errorMessage);
            }

            var payload = await response.Content.ReadFromJsonAsync<OcrResponse>(cancellationToken: cancellationToken);
            if (payload is null)
            {
                return Result<MedicalDocumentExtractionDto>.Failure("OCR service returned empty response.");
            }

            var dto = new MedicalDocumentExtractionDto(
                payload.Text ?? string.Empty,
                "OCR",
                payload.Success,
                payload.Success ? null : payload.Error);

            return Result<MedicalDocumentExtractionDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return Result<MedicalDocumentExtractionDto>.Failure($"OCR request failed: {ex.Message}");
        }
    }

    private sealed class OcrResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("extracted_text")]
        public string? Text { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    private static string GetContentType(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".bmp" => "image/bmp",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }
}
