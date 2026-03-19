namespace ClinicQueue.Application.DTOs;

public sealed record QueueStatusDto(
    string AppointmentId,
    int Position,
    string Status,
    int EstimatedWaitMinutes);
