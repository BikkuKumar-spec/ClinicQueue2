using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;

namespace ClinicQueue.Infrastructure.ExternalServices.AI;

public class SymptomAnalysisGateway(HttpClient httpClient) : ISymptomAnalysisGateway
{
    public async Task<Result<SymptomAnalysisDto>> AnalyzeAsync(string patientId, string symptoms, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("/analyze", new { text = symptoms }, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<SymptomAnalysisDto>.Failure($"AI service returned {(int)response.StatusCode}.");
            }

            var payload = await response.Content.ReadFromJsonAsync<SymptomAnalysisResponse>(cancellationToken: cancellationToken);
            if (payload is null)
            {
                return Result<SymptomAnalysisDto>.Failure("AI service returned empty response.");
            }

            var dto = new SymptomAnalysisDto(
                patientId,
                symptoms,
                payload.RecommendedSpecialty ?? "General Physician",
                payload.Severity ?? "Low",
                payload.Reasoning ?? string.Empty,
                payload.Confidence,
                payload.DetectedLanguage ?? "eng_Latn");

            return Result<SymptomAnalysisDto>.Success(dto);
        }
        catch (Exception ex)
        {
            return Result<SymptomAnalysisDto>.Failure($"AI request failed: {ex.Message}");
        }
    }

    private sealed class SymptomAnalysisResponse
    {
        [JsonPropertyName("recommendedSpecialty")]
        public string? RecommendedSpecialty { get; init; }

        [JsonPropertyName("severity")]
        public string? Severity { get; init; }

        [JsonPropertyName("reasoning")]
        public string? Reasoning { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; }

        [JsonPropertyName("detectedLanguage")]
        public string? DetectedLanguage { get; init; }
    }
}
