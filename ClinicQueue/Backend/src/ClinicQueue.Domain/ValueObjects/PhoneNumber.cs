namespace ClinicQueue.Domain.ValueObjects;

public sealed record PhoneNumber
{
    public string Value { get; }

    public PhoneNumber(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Phone number is required.", nameof(value));
        }

        var normalized = value.Trim();
        Value = normalized;
    }

    public override string ToString() => Value;
}
