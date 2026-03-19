namespace ClinicQueue.Domain.Enums;

public enum QueueStatus
{
    Waiting = 1,
    Called = 2,
    InProgress = 3,
    Done = 4,
    Skipped = 5
}
