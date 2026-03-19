namespace ClinicQueue.Contracts.Appointment;

public sealed record AppointmentResponse(
    string Id,
    string PatientId,
    string DoctorId,
    DateTime SlotTime,
    string Status,
    int? QueuePosition,
    DateTime CreatedAt,
    DateTime UpdatedAt);
