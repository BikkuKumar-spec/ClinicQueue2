namespace ClinicQueue.Contracts.Queue;

public sealed record JoinQueueRequest(
    string AppointmentId,
    long PriorityScore);
