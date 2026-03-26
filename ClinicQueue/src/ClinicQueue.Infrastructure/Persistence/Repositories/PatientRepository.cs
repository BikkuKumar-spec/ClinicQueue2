using ClinicQueue.Application.Interfaces;
using ClinicQueue.Domain.Entities;
using ClinicQueue.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ClinicQueue.Infrastructure.Persistence.Repositories;

public class PatientRepository(ClinicDbContext dbContext)
    : Repository<Patient>(dbContext), IPatientRepository
{
    public async Task<Patient> UpsertByPhoneAsync(string phoneNumber, string patientName, CancellationToken cancellationToken = default)
    {
        var effectiveName = string.IsNullOrWhiteSpace(patientName) ? phoneNumber : patientName.Trim();
        var existing = await GetByPhoneAndNameAsync(phoneNumber, effectiveName, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var patient = Patient.Create(new PersonName(effectiveName), new PhoneNumber(phoneNumber));
        await AddAsync(patient, cancellationToken);
        return patient;
    }

    public async Task<Patient?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        var normalizedPhone = new PhoneNumber(phoneNumber);
        return await DbSet.FirstOrDefaultAsync(
            x => x.PhoneNumber == normalizedPhone,
            cancellationToken);
    }

    public async Task<Patient> GetOrCreateByPhoneNumberAsync(string phoneNumber, string patientName, CancellationToken cancellationToken = default)
    {
        var effectiveName = string.IsNullOrWhiteSpace(patientName) ? phoneNumber : patientName.Trim();
        var existing = await GetByPhoneAndNameAsync(phoneNumber, effectiveName, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var patient = Patient.Create(new PersonName(effectiveName), new PhoneNumber(phoneNumber));
        await AddAsync(patient, cancellationToken);
        return patient;
    }

    private async Task<Patient?> GetByPhoneAndNameAsync(string phoneNumber, string patientName, CancellationToken cancellationToken)
    {
        var normalizedPhone = new PhoneNumber(phoneNumber);
        var candidates = await DbSet
            .Where(x => x.PhoneNumber == normalizedPhone)
            .ToListAsync(cancellationToken);

        return candidates.FirstOrDefault(
            x => string.Equals(x.Name.Value, patientName, StringComparison.OrdinalIgnoreCase));
    }
}
