using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;

namespace ClinicQueue.Application.Interfaces;

public interface ISymptomAnalysisGateway
{
    Task<Result<SymptomAnalysisDto>> AnalyzeAsync(string patientId, string symptoms, CancellationToken cancellationToken = default);
}
