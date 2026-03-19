namespace ClinicQueue.Domain.ValueObjects;

public sealed record PersonName
{
    public string Value { get; }

    public PersonName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Name is required.", nameof(value));
        }

        Value = value.Trim();
    }

    public override string ToString() => Value;
}
