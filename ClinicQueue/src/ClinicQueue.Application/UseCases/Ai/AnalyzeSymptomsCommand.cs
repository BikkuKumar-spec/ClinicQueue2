using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Application.Validators;
using MediatR;

namespace ClinicQueue.Application.UseCases.Ai;

public sealed record AnalyzeSymptomsCommand(string PatientId, string Symptoms)
    : IRequest<Result<SymptomAnalysisDto>>;

public sealed class AnalyzeSymptomsCommandHandler(
    ISymptomAnalysisGateway symptomAnalysisGateway,
    IRequestValidator<AnalyzeSymptomsCommand> validator)
    : IRequestHandler<AnalyzeSymptomsCommand, Result<SymptomAnalysisDto>>
{
    public async Task<Result<SymptomAnalysisDto>> Handle(AnalyzeSymptomsCommand request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<SymptomAnalysisDto>.Failure(string.Join("; ", errors));
        }

        return await symptomAnalysisGateway.AnalyzeAsync(request.PatientId, request.Symptoms, cancellationToken);
    }
}
