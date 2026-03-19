using ClinicQueue.Domain.Enums;

namespace ClinicQueue.Domain.Entities;

public class NotificationReminder
{
    private NotificationReminder()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public string AppointmentId { get; private set; } = string.Empty;
    public NotificationType Type { get; private set; }
    public DateTime ScheduledFor { get; private set; }
    public DateTime? SentAt { get; private set; }
    public bool IsCancelled { get; private set; }

    public static NotificationReminder Schedule(string appointmentId, NotificationType type, DateTime scheduledForUtc)
    {
        if (string.IsNullOrWhiteSpace(appointmentId))
        {
            throw new ArgumentException("AppointmentId is required.", nameof(appointmentId));
        }

        return new NotificationReminder
        {
            Id = Guid.NewGuid().ToString(),
            AppointmentId = appointmentId,
            Type = type,
            ScheduledFor = scheduledForUtc
        };
    }

    public bool IsDue(DateTime atUtc)
    {
        return !IsCancelled && SentAt is null && atUtc >= ScheduledFor;
    }

    public void MarkSent(DateTime sentAtUtc)
    {
        if (IsCancelled)
        {
            throw new InvalidOperationException("Cancelled reminders cannot be marked as sent.");
        }

        SentAt = sentAtUtc;
    }

    public void Reschedule(DateTime newScheduledForUtc)
    {
        if (SentAt is not null)
        {
            throw new InvalidOperationException("Sent reminders cannot be rescheduled.");
        }

        if (IsCancelled)
        {
            throw new InvalidOperationException("Cancelled reminders cannot be rescheduled.");
        }

        ScheduledFor = newScheduledForUtc;
    }

    public void Cancel()
    {
        if (SentAt is not null)
        {
            throw new InvalidOperationException("Sent reminders cannot be cancelled.");
        }

        IsCancelled = true;
    }
}
