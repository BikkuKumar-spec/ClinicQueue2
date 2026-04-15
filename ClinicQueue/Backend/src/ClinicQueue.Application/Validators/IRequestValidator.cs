namespace ClinicQueue.Application.Validators;

public interface IRequestValidator<in TRequest>
{
    IReadOnlyCollection<string> Validate(TRequest request);
}
