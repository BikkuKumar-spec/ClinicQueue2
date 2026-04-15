using ClinicQueue.Application.Interfaces;

namespace ClinicQueue.Infrastructure.Persistence;

public class UnitOfWork(ClinicDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
