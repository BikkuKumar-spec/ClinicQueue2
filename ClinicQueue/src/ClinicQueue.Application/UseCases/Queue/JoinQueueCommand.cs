using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Application.Validators;
using ClinicQueue.Domain.Entities;
using MediatR;

namespace ClinicQueue.Application.UseCases.Queue;

public sealed record JoinQueueCommand(string AppointmentId, long PriorityScore) : IRequest<Result<QueueStatusDto>>;

public sealed class JoinQueueCommandHandler(
    IAppointmentRepository appointmentRepository,
    IQueueRepository queueRepository,
    IUnitOfWork unitOfWork,
    IRequestValidator<JoinQueueCommand> validator)
    : IRequestHandler<JoinQueueCommand, Result<QueueStatusDto>>
{
    public async Task<Result<QueueStatusDto>> Handle(JoinQueueCommand request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<QueueStatusDto>.Failure(string.Join("; ", errors));
        }

        var appointment = await appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
        {
            return Result<QueueStatusDto>.Failure("Appointment not found.");
        }

        var existingQueueEntry = await queueRepository.GetByAppointmentIdAsync(request.AppointmentId, cancellationToken);
        if (existingQueueEntry is not null)
        {
            var existingStatus = new QueueStatusDto(
                existingQueueEntry.AppointmentId,
                existingQueueEntry.Position,
                existingQueueEntry.Status.ToString(),
                (int)existingQueueEntry.GetEstimatedWait(10).TotalMinutes);

            return Result<QueueStatusDto>.Success(existingStatus);
        }

        var currentQueue = await queueRepository.GetCurrentQueueAsync(null, cancellationToken);
        var position = currentQueue.Count + 1;

        var queueEntry = QueueEntry.Create(request.AppointmentId, request.PriorityScore);
        queueEntry.MoveToPosition(position);

        appointment.Confirm();
        appointment.AssignQueuePosition(position);

        await queueRepository.AddAsync(queueEntry, cancellationToken);
        await appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var result = new QueueStatusDto(
            queueEntry.AppointmentId,
            queueEntry.Position,
            queueEntry.Status.ToString(),
            (int)queueEntry.GetEstimatedWait(10).TotalMinutes);

        return Result<QueueStatusDto>.Success(result);
    }
}
