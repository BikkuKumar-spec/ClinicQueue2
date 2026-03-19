namespace ClinicQueue.Contracts.Reports;

public sealed record UploadMedicalDocumentRequest(
    string PatientId,
    string? AppointmentId,
    string FileName,
    string DocumentReference,
    byte[] FileBytes);
