using ClinicQueue.Api.Common;
using ClinicQueue.Api.Mappings;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.UseCases.Queue;
using ClinicQueue.Contracts.Queue;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class QueueController(ISender sender) : ControllerBase
{
    [HttpPost("join")]
    [ProducesResponseType(typeof(QueueStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QueueStatusResponse>> Join([FromBody] JoinQueueRequest request, CancellationToken cancellationToken)
    {
        var command = new JoinQueueCommand(request.AppointmentId, request.PriorityScore);
        var result = await sender.Send(command, cancellationToken);
        return this.ToActionResult(Map(result, dto => dto.ToResponse()));
    }

    [HttpGet("status/{appointmentId}")]
    [ProducesResponseType(typeof(QueueStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QueueStatusResponse>> Status(string appointmentId, CancellationToken cancellationToken)
    {
        var query = new GetQueueStatusQuery(appointmentId);
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
