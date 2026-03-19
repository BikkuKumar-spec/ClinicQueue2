namespace ClinicQueue.Application.DTOs;

public sealed record SymptomAnalysisDto(
    string PatientId,
    string Symptoms,
    string RecommendedSpecialty,
    string Severity,
    string Reasoning,
    double Confidence,
    string DetectedLanguage);
