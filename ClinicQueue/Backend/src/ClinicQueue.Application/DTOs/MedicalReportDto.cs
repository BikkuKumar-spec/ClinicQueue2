namespace ClinicQueue.Application.DTOs;

public sealed record MedicalReportDto(
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
