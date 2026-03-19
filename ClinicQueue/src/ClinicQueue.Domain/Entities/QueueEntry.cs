using ClinicQueue.Domain.Enums;

namespace ClinicQueue.Domain.Entities;

public class QueueEntry
{
    private QueueEntry()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public string AppointmentId { get; private set; } = string.Empty;
    public int Position { get; private set; }
    public long PriorityScore { get; private set; }
    public QueueStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static QueueEntry Create(string appointmentId, long priorityScore)
    {
        if (string.IsNullOrWhiteSpace(appointmentId))
        {
            throw new ArgumentException("AppointmentId is required.", nameof(appointmentId));
        }

        return new QueueEntry
        {
            Id = Guid.NewGuid().ToString(),
            AppointmentId = appointmentId,
            PriorityScore = priorityScore,
            Position = 0,
            Status = QueueStatus.Waiting,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void MoveToPosition(int position)
    {
        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Queue position must be greater than zero.");
        }

        Position = position;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkCalled()
    {
        Status = QueueStatus.Called;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StartProgress()
    {
        Status = QueueStatus.InProgress;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkDone()
    {
        Status = QueueStatus.Done;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Skip()
    {
        Status = QueueStatus.Skipped;
        UpdatedAt = DateTime.UtcNow;
    }

    public TimeSpan GetEstimatedWait(int minutesPerPatient)
    {
        if (minutesPerPatient <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minutesPerPatient), "Minutes per patient must be greater than zero.");
        }

        if (Position <= 1)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromMinutes((Position - 1) * minutesPerPatient);
    }
}
