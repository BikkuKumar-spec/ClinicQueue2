namespace ClinicQueue.Application.DTOs;

public sealed record MedicalDocumentExtractionDto(
    string ExtractedText,
    string Source,
    bool IsSuccess,
    string? ErrorMessage);
