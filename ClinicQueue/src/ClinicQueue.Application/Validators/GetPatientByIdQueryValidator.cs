using ClinicQueue.Application.UseCases.Patients;

namespace ClinicQueue.Application.Validators;

public sealed class GetPatientByIdQueryValidator : IRequestValidator<GetPatientByIdQuery>
{
    public IReadOnlyCollection<string> Validate(GetPatientByIdQuery request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.PatientId))
        {
            errors.Add("PatientId is required.");
        }

        return errors;
    }
}
