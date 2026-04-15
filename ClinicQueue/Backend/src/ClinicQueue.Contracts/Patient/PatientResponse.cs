namespace ClinicQueue.Contracts.Patient;

public sealed record PatientResponse(
    string Id,
    string Name,
    string PhoneNumber,
    string? MedicalHistoryReference,
    DateTime CreatedAt,
    DateTime? LastVisit);
