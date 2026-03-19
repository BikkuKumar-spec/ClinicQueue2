using ClinicQueue.Application.UseCases.Queue;

namespace ClinicQueue.Application.Validators;

public sealed class JoinQueueCommandValidator : IRequestValidator<JoinQueueCommand>
{
    public IReadOnlyCollection<string> Validate(JoinQueueCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.AppointmentId))
        {
            errors.Add("AppointmentId is required.");
        }

        if (request.PriorityScore < 0)
        {
            errors.Add("PriorityScore cannot be negative.");
        }

        return errors;
    }
}
