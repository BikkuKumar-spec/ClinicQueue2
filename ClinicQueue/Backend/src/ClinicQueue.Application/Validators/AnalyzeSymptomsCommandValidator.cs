using ClinicQueue.Application.UseCases.Ai;

namespace ClinicQueue.Application.Validators;

public sealed class AnalyzeSymptomsCommandValidator : IRequestValidator<AnalyzeSymptomsCommand>
{
    public IReadOnlyCollection<string> Validate(AnalyzeSymptomsCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.PatientId))
        {
            errors.Add("PatientId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Symptoms))
        {
            errors.Add("Symptoms are required.");
        }

        return errors;
    }
}
