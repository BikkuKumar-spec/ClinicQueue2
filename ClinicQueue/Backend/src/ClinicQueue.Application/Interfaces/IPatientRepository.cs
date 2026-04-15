using ClinicQueue.Domain.Entities;

namespace ClinicQueue.Application.Interfaces;

public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetByPhoneNumberAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<Patient> GetOrCreateByPhoneNumberAsync(string phoneNumber, string patientName, CancellationToken cancellationToken = default);
}
