using ClinicQueue.Application.Interfaces;
using ClinicQueue.Domain.Entities;
using ClinicQueue.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClinicQueue.Infrastructure.Persistence.Repositories;

public class QueueRepository(ClinicDbContext dbContext)
    : Repository<QueueEntry>(dbContext), IQueueRepository
{
    public async Task<QueueEntry?> GetByAppointmentIdAsync(string appointmentId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(x => x.AppointmentId == appointmentId, cancellationToken);
    }

    public async Task<IReadOnlyCollection<QueueEntry>> GetCurrentQueueAsync(string? doctorId = null, CancellationToken cancellationToken = default)
    {
        var queue = DbSet.AsNoTracking().Where(x => x.Status == QueueStatus.Waiting);

        if (!string.IsNullOrWhiteSpace(doctorId))
        {
            var appointmentIds = await DbContext.Appointments
                .AsNoTracking()
                .Where(x => x.DoctorId == doctorId)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            queue = queue.Where(x => appointmentIds.Contains(x.AppointmentId));
        }

        return await queue
            .OrderBy(x => x.Position)
            .ThenBy(x => x.PriorityScore)
            .ToListAsync(cancellationToken);
    }
}
