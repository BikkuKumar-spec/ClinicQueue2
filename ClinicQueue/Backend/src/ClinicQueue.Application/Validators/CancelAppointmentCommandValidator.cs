using ClinicQueue.Application.UseCases.Appointments;

namespace ClinicQueue.Application.Validators;

public sealed class CancelAppointmentCommandValidator : IRequestValidator<CancelAppointmentCommand>
{
    public IReadOnlyCollection<string> Validate(CancelAppointmentCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.AppointmentId))
        {
            errors.Add("AppointmentId is required.");
        }

        return errors;
    }
}
