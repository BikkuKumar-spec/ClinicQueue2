namespace ClinicQueue.Contracts.Reports;

public sealed record ReportSummaryResponse(
    string MedicalReportId,
    string Summary,
    DateTime GeneratedAt);
