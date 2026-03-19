using ClinicQueue.Domain.Entities;

namespace ClinicQueue.Application.Interfaces;

public interface IDoctorRepository : IRepository<Doctor>
{
    Task<IReadOnlyCollection<string>> GetActiveSpecialtiesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Doctor>> GetAvailableBySpecialtyAsync(string specialty, CancellationToken cancellationToken = default);
    Task<Doctor?> FindAvailableDoctorByNameAsync(string doctorName, string? specialty = null, CancellationToken cancellationToken = default);
}
