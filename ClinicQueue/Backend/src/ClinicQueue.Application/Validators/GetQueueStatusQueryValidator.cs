using ClinicQueue.Application.UseCases.Queue;

namespace ClinicQueue.Application.Validators;

public sealed class GetQueueStatusQueryValidator : IRequestValidator<GetQueueStatusQuery>
{
    public IReadOnlyCollection<string> Validate(GetQueueStatusQuery request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.AppointmentId))
        {
            errors.Add("AppointmentId is required.");
        }

        return errors;
    }
}
