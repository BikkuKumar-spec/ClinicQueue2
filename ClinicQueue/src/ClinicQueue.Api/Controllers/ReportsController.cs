using ClinicQueue.Api.Common;
using ClinicQueue.Api.Mappings;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.UseCases.Ai;
using ClinicQueue.Application.UseCases.Documents;
using ClinicQueue.Application.UseCases.Reports;
using ClinicQueue.Contracts.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class ReportsController(ISender sender) : ControllerBase
{
    [HttpPost("analyze-symptoms")]
    [ProducesResponseType(typeof(AnalyzeSymptomsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AnalyzeSymptomsResponse>> AnalyzeSymptoms([FromBody] AnalyzeSymptomsRequest request, CancellationToken cancellationToken)
    {
        var command = new AnalyzeSymptomsCommand(request.PatientId, request.Symptoms);
        var result = await sender.Send(command, cancellationToken);
        return this.ToActionResult(Map(result, dto => dto.ToResponse()));
    }

    [HttpPost("documents/upload")]
    [ProducesResponseType(typeof(MedicalReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MedicalReportResponse>> UploadDocument([FromBody] UploadMedicalDocumentRequest request, CancellationToken cancellationToken)
    {
        var command = new UploadMedicalDocumentCommand(
            request.PatientId,
            request.AppointmentId,
            request.FileName,
            request.DocumentReference,
            request.FileBytes);

        var result = await sender.Send(command, cancellationToken);
        return this.ToActionResult(Map(result, dto => dto.ToResponse()));
    }

    [HttpPost("summaries/generate")]
    [ProducesResponseType(typeof(ReportSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReportSummaryResponse>> GenerateSummary([FromBody] GenerateReportSummaryRequest request, CancellationToken cancellationToken)
    {
        var command = new GenerateReportSummaryCommand(request.MedicalReportId);
        var result = await sender.Send(command, cancellationToken);
        return this.ToActionResult(Map(result, dto => dto.ToResponse()));
    }

    private static Result<TOutput> Map<TInput, TOutput>(Result<TInput> result, Func<TInput, TOutput> mapper)
    {
        if (!result.IsSuccess || result.Value is null)
        {
            return Result<TOutput>.Failure(result.Error ?? "Request failed.");
        }

        return Result<TOutput>.Success(mapper(result.Value));
    }
}
