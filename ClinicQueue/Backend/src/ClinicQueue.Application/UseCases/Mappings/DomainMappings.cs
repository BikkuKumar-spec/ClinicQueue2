using ClinicQueue.Application.DTOs;
using ClinicQueue.Domain.Entities;

namespace ClinicQueue.Application.UseCases.Mappings;

internal static class DomainMappings
{
    public static AppointmentDto ToDto(this Appointment appointment)
    {
        return new AppointmentDto(
            appointment.Id,
            appointment.PatientId,
            appointment.DoctorId,
            appointment.SlotTime,
            appointment.Status.ToString(),
            appointment.QueuePosition,
            appointment.CreatedAt,
            appointment.UpdatedAt);
    }

    public static PatientDto ToDto(this Patient patient)
    {
        return new PatientDto(
            patient.Id,
            patient.Name.Value,
            patient.PhoneNumber.Value,
            patient.MedicalHistoryReference,
            patient.CreatedAt,
            patient.LastVisit);
    }

    public static MedicalReportDto ToDto(this MedicalReport report)
    {
        return new MedicalReportDto(
            report.Id,
            report.PatientId,
            report.AppointmentId,
            report.FileName,
            report.DocumentReference,
            report.Status.ToString(),
            report.ExtractedText,
            report.FailureReason,
            report.CreatedAt,
            report.ProcessedAt);
    }
}
