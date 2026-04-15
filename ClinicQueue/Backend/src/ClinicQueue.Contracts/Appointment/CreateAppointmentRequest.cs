namespace ClinicQueue.Contracts.Appointment;

public sealed record CreateAppointmentRequest(
    string PatientId,
    string DoctorId,
    DateTime SlotTime);
