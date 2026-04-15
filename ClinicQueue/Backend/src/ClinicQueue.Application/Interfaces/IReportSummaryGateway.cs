using ClinicQueue.Application.Common;

namespace ClinicQueue.Application.Interfaces;

public interface IReportSummaryGateway
{
    Task<Result<string>> GenerateSummaryAsync(string extractedText, CancellationToken cancellationToken = default);
}
