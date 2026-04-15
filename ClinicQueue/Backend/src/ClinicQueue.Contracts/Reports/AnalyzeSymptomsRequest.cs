namespace ClinicQueue.Contracts.Reports;

public sealed record AnalyzeSymptomsRequest(
    string PatientId,
    string Symptoms);
