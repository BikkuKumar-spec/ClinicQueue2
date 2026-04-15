using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Application.Validators;
using MediatR;

namespace ClinicQueue.Application.UseCases.Reports;

public sealed record GenerateReportSummaryCommand(string MedicalReportId)
    : IRequest<Result<ReportSummaryDto>>;

public sealed class GenerateReportSummaryCommandHandler(
    IMedicalReportRepository medicalReportRepository,
    IReportSummaryGateway reportSummaryGateway,
    IRequestValidator<GenerateReportSummaryCommand> validator)
    : IRequestHandler<GenerateReportSummaryCommand, Result<ReportSummaryDto>>
{
    public async Task<Result<ReportSummaryDto>> Handle(GenerateReportSummaryCommand request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<ReportSummaryDto>.Failure(string.Join("; ", errors));
        }

        var report = await medicalReportRepository.GetByIdAsync(request.MedicalReportId, cancellationToken);
        if (report is null)
        {
            return Result<ReportSummaryDto>.Failure("Medical report not found.");
        }

        if (string.IsNullOrWhiteSpace(report.ExtractedText))
        {
            return Result<ReportSummaryDto>.Failure("Medical report has no extracted text.");
        }

        var summaryResult = await reportSummaryGateway.GenerateSummaryAsync(report.ExtractedText, cancellationToken);
        if (!summaryResult.IsSuccess || string.IsNullOrWhiteSpace(summaryResult.Value))
        {
            return Result<ReportSummaryDto>.Failure(summaryResult.Error ?? "Summary generation failed.");
        }

        var dto = new ReportSummaryDto(
            report.Id,
            summaryResult.Value,
            DateTime.UtcNow);

        return Result<ReportSummaryDto>.Success(dto);
    }
}
