using ClinicQueue.Application.UseCases.Documents;

namespace ClinicQueue.Application.Validators;

public sealed class UploadMedicalDocumentCommandValidator : IRequestValidator<UploadMedicalDocumentCommand>
{
    public IReadOnlyCollection<string> Validate(UploadMedicalDocumentCommand request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.PatientId))
        {
            errors.Add("PatientId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            errors.Add("FileName is required.");
        }

        if (string.IsNullOrWhiteSpace(request.DocumentReference))
        {
            errors.Add("DocumentReference is required.");
        }

        if (request.FileBytes is null || request.FileBytes.Length == 0)
        {
            errors.Add("FileBytes are required.");
        }

        return errors;
    }
}
