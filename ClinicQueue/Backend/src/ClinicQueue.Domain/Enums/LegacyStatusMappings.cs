namespace ClinicQueue.Domain.Enums;

public static class LegacyStatusMappings
{
    public static AppointmentStatus ToAppointmentStatus(string legacyStatus)
    {
        return legacyStatus.Trim().ToUpperInvariant() switch
        {
            "BOOKED" => AppointmentStatus.Scheduled,
            "ARRIVED" => AppointmentStatus.Confirmed,
            "IN_QUEUE" => AppointmentStatus.Confirmed,
            "IN_CONSULTATION" => AppointmentStatus.Confirmed,
            "NO_SHOW" => AppointmentStatus.Cancelled,
            "CANCELLED" => AppointmentStatus.Cancelled,
            "COMPLETED" => AppointmentStatus.Completed,
            _ => AppointmentStatus.Scheduled
        };
    }

    public static QueueStatus ToQueueStatus(string legacyStatus)
    {
        return legacyStatus.Trim().ToUpperInvariant() switch
        {
            "IN_QUEUE" => QueueStatus.Waiting,
            "CALLED" => QueueStatus.Called,
            "IN_CONSULTATION" => QueueStatus.InProgress,
            "COMPLETED" => QueueStatus.Done,
            "DONE" => QueueStatus.Done,
            "SKIPPED" => QueueStatus.Skipped,
            _ => QueueStatus.Waiting
        };
    }
}
