using ClinicQueue.Domain.ValueObjects;

namespace ClinicQueue.Domain.Entities;

public class Patient
{
    private Patient()
    {
    }

    public string Id { get; private set; } = string.Empty;
    public PersonName Name { get; private set; } = new("Unknown");
    public PhoneNumber PhoneNumber { get; private set; } = new("Unknown");
    public string? MedicalHistoryReference { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastVisit { get; private set; }

    public static Patient Create(PersonName name, PhoneNumber phoneNumber, string? medicalHistoryReference = null)
    {
        return new Patient
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            PhoneNumber = phoneNumber,
            MedicalHistoryReference = string.IsNullOrWhiteSpace(medicalHistoryReference) ? null : medicalHistoryReference.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Rename(PersonName newName)
    {
        Name = newName;
    }

    public void UpdatePhone(PhoneNumber newPhoneNumber)
    {
        PhoneNumber = newPhoneNumber;
    }

    public void LinkMedicalHistory(string medicalHistoryReference)
    {
        if (string.IsNullOrWhiteSpace(medicalHistoryReference))
        {
            throw new ArgumentException("Medical history reference is required.", nameof(medicalHistoryReference));
        }

        MedicalHistoryReference = medicalHistoryReference.Trim();
    }

    public void RecordVisit(DateTime visitTimeUtc)
    {
        LastVisit = visitTimeUtc;
    }
}
