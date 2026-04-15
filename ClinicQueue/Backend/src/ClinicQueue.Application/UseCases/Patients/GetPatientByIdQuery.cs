using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Application.UseCases.Mappings;
using ClinicQueue.Application.Validators;
using MediatR;

namespace ClinicQueue.Application.UseCases.Patients;

public sealed record GetPatientByIdQuery(string PatientId) : IRequest<Result<PatientDto>>;

public sealed class GetPatientByIdQueryHandler(
    IPatientRepository patientRepository,
    IRequestValidator<GetPatientByIdQuery> validator)
    : IRequestHandler<GetPatientByIdQuery, Result<PatientDto>>
{
    public async Task<Result<PatientDto>> Handle(GetPatientByIdQuery request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<PatientDto>.Failure(string.Join("; ", errors));
        }

        var patient = await patientRepository.GetByIdAsync(request.PatientId, cancellationToken);
        if (patient is null)
        {
            return Result<PatientDto>.Failure("Patient not found.");
        }

        return Result<PatientDto>.Success(patient.ToDto());
    }
}
