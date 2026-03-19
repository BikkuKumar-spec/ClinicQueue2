using ClinicQueue.Application.UseCases.Reports;

namespace ClinicQueue.Application.Validators;

public sealed class GenerateReportSummaryCommandValidator : IRequestValidator<GenerateReportSummaryCommand>
{
    public IReadOnlyCollection<string> Validate(GenerateReportSummaryCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.MedicalReportId))
        {
            errors.Add("MedicalReportId is required.");
        }

        return errors;
    }
}
