namespace ClinicQueue.Application.DTOs;

public sealed record ReportSummaryDto(
    string MedicalReportId,
    string Summary,
    DateTime GeneratedAt);
