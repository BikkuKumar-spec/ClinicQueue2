using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Application.UseCases.Mappings;
using ClinicQueue.Application.Validators;
using ClinicQueue.Domain.Entities;
using MediatR;

namespace ClinicQueue.Application.UseCases.Appointments;

public sealed record CreateAppointmentCommand(
    string PatientId,
    string DoctorId,
    DateTime SlotTime) : IRequest<Result<AppointmentDto>>;

public sealed class CreateAppointmentCommandHandler(
    IAppointmentRepository appointmentRepository,
    IUnitOfWork unitOfWork,
    IRequestValidator<CreateAppointmentCommand> validator)
    : IRequestHandler<CreateAppointmentCommand, Result<AppointmentDto>>
{
    public async Task<Result<AppointmentDto>> Handle(CreateAppointmentCommand request, CancellationToken cancellationToken)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
        {
            return Result<AppointmentDto>.Failure(string.Join("; ", errors));
        }

        var available = await appointmentRepository.IsDoctorSlotAvailableAsync(
            request.DoctorId,
            request.SlotTime,
            cancellationToken);

        if (!available)
        {
            return Result<AppointmentDto>.Failure("Selected slot is not available.");
        }

        var appointment = Appointment.Create(request.PatientId, request.DoctorId, request.SlotTime);
        await appointmentRepository.AddAsync(appointment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AppointmentDto>.Success(appointment.ToDto());
    }
}
