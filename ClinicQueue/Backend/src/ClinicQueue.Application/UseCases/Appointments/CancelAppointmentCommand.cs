using ClinicQueue.Application.Common;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Application.Validators;
using MediatR;

namespace ClinicQueue.Application.UseCases.Appointments;

public sealed record CancelAppointmentCommand(string AppointmentId) : IRequest<Result<bool>>;

public sealed class CancelAppointmentCommandHandler(
    IAppointmentRepository appointmentRepository,
    IUnitOfWork unitOfWork,
    IRequestValidator<CancelAppointmentCommand> validator)
    : IRequestHandler<CancelAppointmentCommand, Result<bool>>
{
    public async Task<Result<bool>> Handle(CancelAppointmentCommand request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<bool>.Failure(string.Join("; ", errors));
        }

        var appointment = await appointmentRepository.GetByIdAsync(request.AppointmentId, cancellationToken);
        if (appointment is null)
        {
            return Result<bool>.Failure("Appointment not found.");
        }

        appointment.Cancel();
        await appointmentRepository.UpdateAsync(appointment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
