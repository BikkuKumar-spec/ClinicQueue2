namespace ClinicQueue.Contracts.Reports;

public sealed record MedicalReportResponse(
    string Id,
    string PatientId,
    string? AppointmentId,
    string FileName,
    string DocumentReference,
    string Status,
    string? ExtractedText,
    string? FailureReason,
    DateTime CreatedAt,
    DateTime? ProcessedAt);
