using ClinicQueue.Domain.Enums;

namespace ClinicQueue.Domain.Entities;

public class Appointment
{
    private Appointment()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public string PatientId { get; private set; } = string.Empty;
    public string DoctorId { get; private set; } = string.Empty;
    public DateTime SlotTime { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public int? QueuePosition { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public static Appointment Create(string patientId, string doctorId, DateTime slotTimeUtc)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            throw new ArgumentException("PatientId is required.", nameof(patientId));
        }

        if (string.IsNullOrWhiteSpace(doctorId))
        {
            throw new ArgumentException("DoctorId is required.", nameof(doctorId));
        }

        return new Appointment
        {
            Id = Guid.NewGuid().ToString(),
            PatientId = patientId,
            DoctorId = doctorId,
            SlotTime = slotTimeUtc,
            Status = AppointmentStatus.Scheduled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void Confirm()
    {
        if (Status == AppointmentStatus.Cancelled || Status == AppointmentStatus.Completed)
        {
            throw new InvalidOperationException("Cancelled or completed appointments cannot be confirmed.");
        }

        Status = AppointmentStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == AppointmentStatus.Completed)
        {
            throw new InvalidOperationException("Completed appointments cannot be cancelled.");
        }

        Status = AppointmentStatus.Cancelled;
        QueuePosition = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        if (Status == AppointmentStatus.Cancelled)
        {
            throw new InvalidOperationException("Cancelled appointments cannot be completed.");
        }

        Status = AppointmentStatus.Completed;
        QueuePosition = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reschedule(DateTime newSlotTimeUtc)
    {
        if (Status == AppointmentStatus.Completed)
        {
            throw new InvalidOperationException("Completed appointments cannot be rescheduled.");
        }

        SlotTime = newSlotTimeUtc;
        Status = AppointmentStatus.Scheduled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AssignQueuePosition(int position)
    {
        if (position <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(position), "Queue position must be greater than zero.");
        }

        QueuePosition = position;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ClearQueuePosition()
    {
        QueuePosition = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
