using ClinicQueue.Application.UseCases.Appointments;

namespace ClinicQueue.Application.Validators;

public sealed class CreateAppointmentCommandValidator : IRequestValidator<CreateAppointmentCommand>
{
    public IReadOnlyCollection<string> Validate(CreateAppointmentCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.PatientId))
        {
            errors.Add("PatientId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DoctorId))
        {
            errors.Add("DoctorId is required.");
        }

        if (request.SlotTime <= DateTime.UtcNow.AddMinutes(-1))
        {
            errors.Add("SlotTime must be in the future.");
        }

        return errors;
    }
}
