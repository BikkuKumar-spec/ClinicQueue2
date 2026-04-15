namespace ClinicQueue.Application.DTOs;

public sealed record PatientDto(
    string Id,
    string Name,
    string PhoneNumber,
    string? MedicalHistoryReference,
    DateTime CreatedAt,
    DateTime? LastVisit);
