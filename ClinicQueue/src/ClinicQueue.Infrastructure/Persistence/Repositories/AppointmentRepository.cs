using ClinicQueue.Application.Interfaces;
using ClinicQueue.Domain.Entities;
using ClinicQueue.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClinicQueue.Infrastructure.Persistence.Repositories;

public class AppointmentRepository(ClinicDbContext dbContext)
    : Repository<Appointment>(dbContext), IAppointmentRepository
{
    public async Task<IReadOnlyCollection<DateTime>> GetBookedSlotsForDoctorOnDateAsync(
        string doctorId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        return await DbSet
            .AsNoTracking()
            .Where(x => x.DoctorId == doctorId
                        && x.SlotTime >= dayStart
                        && x.SlotTime < dayEnd
                        && x.Status != AppointmentStatus.Cancelled)
            .Select(x => x.SlotTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<DateTime?> FindNextAvailableSlotOnDateAsync(
        string doctorId,
        DateTime date,
        int startHour,
        int endHour,
        int slotIntervalMinutes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doctorId)
            || slotIntervalMinutes <= 0
            || endHour <= startHour)
        {
            return null;
        }

        var now = DateTime.Now;
        var dayStart = date.Date.AddHours(startHour);
        var dayEnd = date.Date.AddHours(endHour);
        var firstCandidate = AlignToNextSlotBoundary(dayStart > now ? dayStart : now, slotIntervalMinutes);

        var booked = (await GetBookedSlotsForDoctorOnDateAsync(doctorId, date, cancellationToken)).ToHashSet();

        for (var candidate = firstCandidate; candidate < dayEnd; candidate = candidate.AddMinutes(slotIntervalMinutes))
        {
            if (!booked.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public async Task<DateTime?> FindNextAvailableSlotWithinWindowAsync(
        string doctorId,
        DateTime from,
        int bookingWindowDays,
        int startHour,
        int endHour,
        int slotIntervalMinutes,
        CancellationToken cancellationToken = default)
    {
        if (bookingWindowDays <= 0)
        {
            return null;
        }

        var startDate = from.Date;
        var endDate = startDate.AddDays(bookingWindowDays);

        for (var day = startDate; day < endDate; day = day.AddDays(1))
        {
            var slot = await FindNextAvailableSlotOnDateAsync(
                doctorId,
                day,
                startHour,
                endHour,
                slotIntervalMinutes,
                cancellationToken);

            if (slot.HasValue)
            {
                return slot.Value;
            }
        }

        return null;
    }

    public Task AddReminderAsync(NotificationReminder reminder, CancellationToken cancellationToken = default)
    {
        DbContext.NotificationReminders.Add(reminder);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<Appointment>> GetByPatientIdAsync(string patientId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.SlotTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IsDoctorSlotAvailableAsync(string doctorId, DateTime slotTime, CancellationToken cancellationToken = default)
    {
        var occupied = await DbSet.AnyAsync(
            x => x.DoctorId == doctorId
                && x.SlotTime == slotTime
                && x.Status != AppointmentStatus.Cancelled,
            cancellationToken);

        return !occupied;
    }

    public async Task<int> GetActiveBookingCountInSlotBatchAsync(
        string doctorId,
        DateTime slotTime,
        int batchMinutes,
        CancellationToken cancellationToken = default)
    {
        if (batchMinutes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchMinutes), "Batch duration must be greater than zero.");
        }

        var batchStart = new DateTime(
            slotTime.Year,
            slotTime.Month,
            slotTime.Day,
            slotTime.Hour,
            (slotTime.Minute / batchMinutes) * batchMinutes,
            0,
            slotTime.Kind);
        var batchEnd = batchStart.AddMinutes(batchMinutes);

        return await DbSet
            .AsNoTracking()
            .CountAsync(
                x => x.DoctorId == doctorId
                    && x.SlotTime >= batchStart
                    && x.SlotTime < batchEnd
                    && x.Status != AppointmentStatus.Cancelled,
                cancellationToken);
    }

    public async Task<DateTime?> GetNextAvailableSlotAsync(
        string doctorId,
        DateTime fromTime,
        int slotIntervalMinutes,
        int lookAheadDays,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(doctorId) || slotIntervalMinutes <= 0 || lookAheadDays <= 0)
        {
            return null;
        }

        var start = AlignToNextSlotBoundary(fromTime, slotIntervalMinutes);
        var end = start.Date.AddDays(lookAheadDays);

        var bookedSlots = await DbSet
            .AsNoTracking()
            .Where(x => x.DoctorId == doctorId
                        && x.SlotTime >= start
                        && x.SlotTime < end
                        && x.Status != AppointmentStatus.Cancelled)
            .Select(x => x.SlotTime)
            .ToListAsync(cancellationToken);

        var occupied = bookedSlots.ToHashSet();
        for (var candidate = start; candidate < end; candidate = candidate.AddMinutes(slotIntervalMinutes))
        {
            if (!occupied.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public async Task<int> GetNextQueuePositionAsync(DateTime slotDate, string doctorId, CancellationToken cancellationToken = default)
    {
        var dayStart = slotDate.Date;
        var dayEnd = dayStart.AddDays(1);

        var maxQueue = await DbSet
            .AsNoTracking()
            .Where(x => x.DoctorId == doctorId
                        && x.SlotTime >= dayStart
                        && x.SlotTime < dayEnd
                        && x.Status != AppointmentStatus.Cancelled
                        && x.QueuePosition.HasValue)
            .MaxAsync(x => (int?)x.QueuePosition, cancellationToken);

        return (maxQueue ?? 0) + 1;
    }

    private static DateTime AlignToNextSlotBoundary(DateTime fromTime, int slotIntervalMinutes)
    {
        var baseTime = new DateTime(
            fromTime.Year,
            fromTime.Month,
            fromTime.Day,
            fromTime.Hour,
            fromTime.Minute,
            0,
            fromTime.Kind);

        var remainder = baseTime.Minute % slotIntervalMinutes;
        if (remainder == 0 && fromTime.Second == 0)
        {
            return baseTime;
        }

        var delta = slotIntervalMinutes - remainder;
        return baseTime.AddMinutes(delta);
    }
}
