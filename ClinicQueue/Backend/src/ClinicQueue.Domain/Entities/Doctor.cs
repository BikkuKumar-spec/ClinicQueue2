using ClinicQueue.Domain.ValueObjects;

namespace ClinicQueue.Domain.Entities;

public class Doctor
{
    private readonly List<ScheduleWindow> _availability = [];

    private Doctor()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public PersonName Name { get; private set; } = new("Unknown");
    public string Specialty { get; private set; } = string.Empty;
    public bool IsAvailable { get; private set; }
    public IReadOnlyCollection<ScheduleWindow> Availability => _availability.AsReadOnly();

    public static Doctor Create(PersonName name, string specialty, IEnumerable<ScheduleWindow>? availability = null)
    {
        if (string.IsNullOrWhiteSpace(specialty))
        {
            throw new ArgumentException("Specialty is required.", nameof(specialty));
        }

        var doctor = new Doctor
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Specialty = specialty.Trim(),
            IsAvailable = true
        };

        if (availability is not null)
        {
            doctor._availability.AddRange(availability);
        }

        return doctor;
    }

    public void SetAvailability(bool isAvailable)
    {
        IsAvailable = isAvailable;
    }

    public void UpdateSpecialty(string specialty)
    {
        if (string.IsNullOrWhiteSpace(specialty))
        {
            throw new ArgumentException("Specialty is required.", nameof(specialty));
        }

        Specialty = specialty.Trim();
    }

    public void ReplaceAvailability(IEnumerable<ScheduleWindow> windows)
    {
        _availability.Clear();
        _availability.AddRange(windows);
    }

    public bool CanAcceptAppointmentAt(DateTime slotTimeUtc)
    {
        if (!IsAvailable)
        {
            return false;
        }

        if (_availability.Count == 0)
        {
            return true;
        }

        return _availability.Any(window => window.Contains(slotTimeUtc));
    }
}
