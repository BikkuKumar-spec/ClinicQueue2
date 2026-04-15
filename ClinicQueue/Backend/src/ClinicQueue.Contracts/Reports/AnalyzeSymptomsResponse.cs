namespace ClinicQueue.Contracts.Reports;

public sealed record AnalyzeSymptomsResponse(
    string PatientId,
    string Symptoms,
    string RecommendedSpecialty,
    string Severity,
    string Reasoning,
    double Confidence,
    string DetectedLanguage);
