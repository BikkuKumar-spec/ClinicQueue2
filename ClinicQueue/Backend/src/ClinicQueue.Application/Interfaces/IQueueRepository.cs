using ClinicQueue.Domain.Entities;

namespace ClinicQueue.Application.Interfaces;

public interface IQueueRepository : IRepository<QueueEntry>
{
    Task<QueueEntry?> GetByAppointmentIdAsync(string appointmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<QueueEntry>> GetCurrentQueueAsync(string? doctorId = null, CancellationToken cancellationToken = default);
}
