using ClinicQueue.Application.DTOs;
using ClinicQueue.Contracts.Appointment;
using ClinicQueue.Contracts.Auth;
using ClinicQueue.Contracts.Patient;
using ClinicQueue.Contracts.Queue;
using ClinicQueue.Contracts.Reports;

namespace ClinicQueue.Api.Mappings;

public static class ResponseMappings
{
    public static AuthTokenResponse ToResponse(this AuthTokenDto dto) =>
        new(dto.AccessToken, dto.ExpiresAt, dto.UserId, dto.Role);

    public static AppointmentResponse ToResponse(this AppointmentDto dto) =>
        new(dto.Id, dto.PatientId, dto.DoctorId, dto.SlotTime, dto.Status, dto.QueuePosition, dto.CreatedAt, dto.UpdatedAt);

    public static PatientResponse ToResponse(this PatientDto dto) =>
        new(dto.Id, dto.Name, dto.PhoneNumber, dto.MedicalHistoryReference, dto.CreatedAt, dto.LastVisit);

    public static QueueStatusResponse ToResponse(this QueueStatusDto dto) =>
        new(dto.AppointmentId, dto.Position, dto.Status, dto.EstimatedWaitMinutes);

    public static AnalyzeSymptomsResponse ToResponse(this SymptomAnalysisDto dto) =>
        new(dto.PatientId, dto.Symptoms, dto.RecommendedSpecialty, dto.Severity, dto.Reasoning, dto.Confidence, dto.DetectedLanguage);

    public static MedicalReportResponse ToResponse(this MedicalReportDto dto) =>
        new(
            dto.Id,
            dto.PatientId,
            dto.AppointmentId,
            dto.FileName,
            dto.DocumentReference,
            dto.Status,
            dto.ExtractedText,
            dto.FailureReason,
            dto.CreatedAt,
            dto.ProcessedAt);

    public static ReportSummaryResponse ToResponse(this ReportSummaryDto dto) =>
        new(dto.MedicalReportId, dto.Summary, dto.GeneratedAt);
}
