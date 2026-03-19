using ClinicQueue.Api.Common;
using ClinicQueue.Api.Mappings;
using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.UseCases.Appointments;
using ClinicQueue.Contracts.Appointment;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class AppointmentsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(AppointmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AppointmentResponse>> Create([FromBody] CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateAppointmentCommand(request.PatientId, request.DoctorId, request.SlotTime);
        var result = await sender.Send(command, cancellationToken);
        return this.ToActionResult(Map(result, dto => dto.ToResponse()));
    }

    [HttpPost("cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Cancel([FromBody] CancelAppointmentRequest request, CancellationToken cancellationToken)
    {
        var command = new CancelAppointmentCommand(request.AppointmentId);
        var result = await sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new { cancelled = true });
        }

        return this.ToActionResult(Result<object>.Failure(result.Error ?? "Unable to cancel appointment."));
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
