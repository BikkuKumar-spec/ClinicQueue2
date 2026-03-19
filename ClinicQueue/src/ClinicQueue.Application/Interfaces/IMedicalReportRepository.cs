using ClinicQueue.Domain.Entities;

namespace ClinicQueue.Application.Interfaces;

public interface IMedicalReportRepository : IRepository<MedicalReport>
{
    Task<IReadOnlyCollection<MedicalReport>> GetByPatientIdAsync(string patientId, CancellationToken cancellationToken = default);
}
