using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Application.Validators;
using MediatR;

namespace ClinicQueue.Application.UseCases.Queue;

public sealed record GetQueueStatusQuery(string AppointmentId) : IRequest<Result<QueueStatusDto>>;

public sealed class GetQueueStatusQueryHandler(
    IQueueRepository queueRepository,
    IRequestValidator<GetQueueStatusQuery> validator)
    : IRequestHandler<GetQueueStatusQuery, Result<QueueStatusDto>>
{
    public async Task<Result<QueueStatusDto>> Handle(GetQueueStatusQuery request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<QueueStatusDto>.Failure(string.Join("; ", errors));
        }

        var queueEntry = await queueRepository.GetByAppointmentIdAsync(request.AppointmentId, cancellationToken);
        if (queueEntry is null)
        {
            return Result<QueueStatusDto>.Failure("Queue entry not found.");
        }

        var dto = new QueueStatusDto(
            queueEntry.AppointmentId,
            queueEntry.Position,
            queueEntry.Status.ToString(),
            (int)queueEntry.GetEstimatedWait(10).TotalMinutes);

        return Result<QueueStatusDto>.Success(dto);
    }
}
