namespace ClinicQueue.Domain.ValueObjects;

public sealed record ScheduleWindow
{
    public TimeOnly Start { get; }
    public TimeOnly End { get; }

    public ScheduleWindow(TimeOnly start, TimeOnly end)
    {
        if (end <= start)
        {
            throw new ArgumentException("End time must be after start time.");
        }

        Start = start;
        End = end;
    }

    public bool Contains(DateTime timestamp)
    {
        var time = TimeOnly.FromDateTime(timestamp);
        return time >= Start && time <= End;
    }
}
