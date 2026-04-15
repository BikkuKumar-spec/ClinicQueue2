namespace ClinicQueue.Application.DTOs;

public sealed record AppointmentDto(
    string Id,
    string PatientId,
    string DoctorId,
    DateTime SlotTime,
    string Status,
    int? QueuePosition,
    DateTime CreatedAt,
    DateTime UpdatedAt);
