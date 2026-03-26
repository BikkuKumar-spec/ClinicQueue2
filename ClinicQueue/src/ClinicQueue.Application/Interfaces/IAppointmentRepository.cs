using ClinicQueue.Domain.Entities;

namespace ClinicQueue.Application.Interfaces;

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<IReadOnlyCollection<Appointment>> GetByPatientIdAsync(string patientId, CancellationToken cancellationToken = default);
    Task<bool> IsDoctorSlotAvailableAsync(string doctorId, DateTime slotTime, CancellationToken cancellationToken = default);
    Task<int> GetActiveBookingCountInSlotBatchAsync(
        string doctorId,
        DateTime slotTime,
        int batchMinutes,
        CancellationToken cancellationToken = default);
    Task<DateTime?> GetNextAvailableSlotAsync(
        string doctorId,
        DateTime fromTime,
        int slotIntervalMinutes,
        int lookAheadDays,
        CancellationToken cancellationToken = default);
    Task<int> GetNextQueuePositionAsync(DateTime slotDate, string doctorId, CancellationToken cancellationToken = default);
}
