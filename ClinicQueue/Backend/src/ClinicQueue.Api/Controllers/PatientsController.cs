using ClinicQueue.Api.Common;
using ClinicQueue.Api.Mappings;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.UseCases.Patients;
using ClinicQueue.Contracts.Patient;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class PatientsController(ISender sender) : ControllerBase
{
    [HttpGet("{patientId}")]
    [ProducesResponseType(typeof(PatientResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PatientResponse>> GetById(string patientId, CancellationToken cancellationToken)
    {
        var query = new GetPatientByIdQuery(patientId);
        var result = await sender.Send(query, cancellationToken);
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
