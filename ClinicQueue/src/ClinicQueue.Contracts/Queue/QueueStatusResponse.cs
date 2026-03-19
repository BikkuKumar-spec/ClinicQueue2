namespace ClinicQueue.Contracts.Queue;

public sealed record QueueStatusResponse(
    string AppointmentId,
    int Position,
    string Status,
    int EstimatedWaitMinutes);
