using ClinicQueue.Domain.Enums;

namespace ClinicQueue.Domain.Entities;

public class MedicalReport
{
    private MedicalReport()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public string PatientId { get; private set; } = string.Empty;
    public string? AppointmentId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string DocumentReference { get; private set; } = string.Empty;
    public MedicalReportStatus Status { get; private set; }
    public string? ExtractedText { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }

    public static MedicalReport Create(string patientId, string? appointmentId, string fileName, string documentReference)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("PatientId is required.", nameof(patientId));
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("FileName is required.", nameof(fileName));
        }

        if (string.IsNullOrWhiteSpace(documentReference))
        {
            throw new ArgumentException("Document reference is required.", nameof(documentReference));
        }

        return new MedicalReport
        {
            Id = Guid.NewGuid().ToString(),
            PatientId = patientId,
            AppointmentId = string.IsNullOrWhiteSpace(appointmentId) ? null : appointmentId.Trim(),
            FileName = fileName.Trim(),
            DocumentReference = documentReference.Trim(),
            Status = MedicalReportStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkExtracted(string extractedText)
    {
        if (string.IsNullOrWhiteSpace(extractedText))
        {
            throw new ArgumentException("Extracted text cannot be empty.", nameof(extractedText));
        }

        ExtractedText = extractedText;
        FailureReason = null;
        Status = MedicalReportStatus.Extracted;
        ProcessedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Failure reason is required.", nameof(reason));
        }

        FailureReason = reason.Trim();
        Status = MedicalReportStatus.Failed;
        ProcessedAt = DateTime.UtcNow;
    }
}
