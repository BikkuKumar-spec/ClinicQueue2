using ClinicQueue.Application.Interfaces;
using ClinicQueue.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClinicQueue.Infrastructure.Persistence.Repositories;

public class MedicalReportRepository(ClinicDbContext dbContext)
    : Repository<MedicalReport>(dbContext), IMedicalReportRepository
{
    public async Task<IReadOnlyCollection<MedicalReport>> GetByPatientIdAsync(string patientId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
