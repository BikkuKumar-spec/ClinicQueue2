using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Application.UseCases.Mappings;
using ClinicQueue.Application.Validators;
using ClinicQueue.Domain.Entities;
using MediatR;

namespace ClinicQueue.Application.UseCases.Documents;

public sealed record UploadMedicalDocumentCommand(
    string PatientId,
    string? AppointmentId,
    string FileName,
    string DocumentReference,
    byte[] FileBytes) : IRequest<Result<MedicalReportDto>>;

public sealed class UploadMedicalDocumentCommandHandler(
    IMedicalReportRepository medicalReportRepository,
    IDocumentProcessingGateway documentProcessingGateway,
    IUnitOfWork unitOfWork,
    IRequestValidator<UploadMedicalDocumentCommand> validator)
    : IRequestHandler<UploadMedicalDocumentCommand, Result<MedicalReportDto>>
{
    public async Task<Result<MedicalReportDto>> Handle(UploadMedicalDocumentCommand request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<MedicalReportDto>.Failure(string.Join("; ", errors));
        }

        var report = MedicalReport.Create(
            request.PatientId,
            request.AppointmentId,
            request.FileName,
            request.DocumentReference);

        var extraction = await documentProcessingGateway.ExtractAsync(request.FileBytes, request.FileName, cancellationToken);
        if (extraction.IsSuccess && extraction.Value is not null && extraction.Value.IsSuccess)
        {
            report.MarkExtracted(extraction.Value.ExtractedText);
        }
        else
        {
            report.MarkFailed(extraction.Error ?? extraction.Value?.ErrorMessage ?? "Document extraction failed.");
        }

        await medicalReportRepository.AddAsync(report, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<MedicalReportDto>.Success(report.ToDto());
    }
}
