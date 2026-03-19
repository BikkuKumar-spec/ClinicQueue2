using ClinicQueue.Domain.Entities;
using ClinicQueue.Domain.Enums;
using ClinicQueue.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(ClinicDbContext dbContext) : ControllerBase
{
    [HttpGet("appointments/today")]
    public async Task<IActionResult> GetTodayAppointments([FromQuery] DateTime? date, CancellationToken cancellationToken)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        var start = targetDate;
        var end = targetDate.AddDays(1);

        var response = await BuildAppointmentsProjection(start, end, cancellationToken);
        return Ok(response);
    }

    [HttpPatch("appointments/{id}/arrive")]
    public async Task<IActionResult> MarkArrived(string id, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (appointment is null)
        {
            return NotFound();
        }

        appointment.Confirm();

        var queueEntry = await dbContext.QueueEntries.FirstOrDefaultAsync(x => x.AppointmentId == id, cancellationToken);
        if (queueEntry is null)
        {
            var nextPosition = await dbContext.QueueEntries
                .Where(x => x.Status == QueueStatus.Waiting || x.Status == QueueStatus.Called || x.Status == QueueStatus.InProgress)
                .Select(x => (int?)x.Position)
                .MaxAsync(cancellationToken) ?? 0;

            queueEntry = QueueEntry.Create(id, DateTime.UtcNow.Ticks);
            queueEntry.MoveToPosition(nextPosition + 1);
            await dbContext.QueueEntries.AddAsync(queueEntry, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, message = "Patient marked as arrived and added to queue" });
    }

    [HttpPatch("appointments/{id}/no-show")]
    public async Task<IActionResult> MarkNoShow(string id, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (appointment is null)
        {
            return NotFound();
        }

        appointment.Cancel();

        var queueEntry = await dbContext.QueueEntries.FirstOrDefaultAsync(x => x.AppointmentId == id, cancellationToken);
        if (queueEntry is not null)
        {
            queueEntry.Skip();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, message = "Patient marked as no-show" });
    }

    [HttpPatch("appointments/{id}/start-consultation")]
    public async Task<IActionResult> StartConsultation(string id, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (appointment is null)
        {
            return NotFound();
        }

        appointment.Confirm();

        var queueEntry = await dbContext.QueueEntries.FirstOrDefaultAsync(x => x.AppointmentId == id, cancellationToken);
        if (queueEntry is null)
        {
            queueEntry = QueueEntry.Create(id, DateTime.UtcNow.Ticks);
            queueEntry.MoveToPosition(1);
            await dbContext.QueueEntries.AddAsync(queueEntry, cancellationToken);
        }

        queueEntry.StartProgress();

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, message = "Consultation started" });
    }

    [HttpPatch("appointments/{id}/complete")]
    public async Task<IActionResult> CompleteAppointment(string id, CancellationToken cancellationToken)
    {
        var appointment = await dbContext.Appointments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (appointment is null)
        {
            return NotFound();
        }

        appointment.Complete();

        var queueEntry = await dbContext.QueueEntries.FirstOrDefaultAsync(x => x.AppointmentId == id, cancellationToken);
        if (queueEntry is not null)
        {
            queueEntry.MarkDone();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new { success = true, message = "Appointment completed" });
    }

    [HttpGet("slots")]
    public async Task<IActionResult> GetDashboardSlots(
        [FromQuery] DateTime? date,
        [FromQuery] string? specialty,
        [FromQuery] string? doctorName,
        CancellationToken cancellationToken)
    {
        var targetDate = (date ?? DateTime.Today).Date;
        var start = targetDate.AddHours(9);
        var end = targetDate.AddHours(18);

        var rows = await BuildAppointmentsProjection(start, end, cancellationToken);

        if (!string.IsNullOrWhiteSpace(specialty))
        {
            rows = rows.Where(x => string.Equals(x.Specialty, specialty, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(doctorName))
        {
            rows = rows.Where(x => string.Equals(x.DoctorName, doctorName, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var slotGroups = rows
            .GroupBy(x => GetBatchStart(x.SlotTime))
            .OrderBy(x => x.Key)
            .Select(group => new
            {
                time = group.Key,
                isBooked = group.Any(),
                bookingsInBatch = group.Count(),
                maxBookingsPerBatch = 5,
                patients = group.Select(x => new
                {
                    appointmentId = x.AppointmentId,
                    patientName = x.PatientName,
                    phoneNumber = x.PhoneNumber,
                    status = x.Status,
                    doctorName = x.DoctorName,
                    specialty = x.Specialty,
                    queuePosition = x.QueuePosition,
                    symptomSummary = (string?)null,
                    symptomSeverity = (string?)null
                }).ToList()
            })
            .ToList();

        return Ok(slotGroups);
    }

    [HttpGet("queue/live")]
    public async Task<IActionResult> GetLiveQueue([FromQuery] string? doctorName, CancellationToken cancellationToken)
    {
        var queue = await dbContext.QueueEntries.AsNoTracking().ToListAsync(cancellationToken);
        var activeQueue = queue
            .Where(q => q.Status == QueueStatus.Waiting || q.Status == QueueStatus.Called || q.Status == QueueStatus.InProgress)
            .OrderBy(q => q.Position)
            .ToList();

        if (activeQueue.Count == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var appointmentIds = activeQueue.Select(x => x.AppointmentId).Distinct().ToList();
        var appointments = await dbContext.Appointments
            .AsNoTracking()
            .Where(x => appointmentIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var patientIds = appointments.Select(x => x.PatientId).Distinct().ToList();
        var patients = await dbContext.Patients
            .AsNoTracking()
            .Where(x => patientIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var doctors = await dbContext.Doctors.AsNoTracking().ToListAsync(cancellationToken);

        var appointmentMap = appointments.ToDictionary(x => x.Id, x => x);
        var patientMap = patients.ToDictionary(x => x.Id, x => x);
        var doctorMap = doctors.ToDictionary(x => x.Id, x => x);

        var response = activeQueue
            .Where(q => appointmentMap.ContainsKey(q.AppointmentId))
            .Select(q =>
            {
                var appointment = appointmentMap[q.AppointmentId];
                patientMap.TryGetValue(appointment.PatientId, out var patient);
                doctorMap.TryGetValue(appointment.DoctorId, out var doctor);

                return new
                {
                    id = q.Id,
                    appointmentId = q.AppointmentId,
                    position = q.Position,
                    priorityScore = q.PriorityScore,
                    patientName = patient?.Name.Value ?? "Unknown",
                    phoneNumber = patient?.PhoneNumber.Value ?? string.Empty,
                    slotTime = appointment.SlotTime,
                    doctorName = doctor?.Name.Value ?? string.Empty,
                    estimatedWait = Math.Max(0, (q.Position - 1) * 10)
                };
            })
            .Where(item => string.IsNullOrWhiteSpace(doctorName)
                || string.Equals(item.doctorName, doctorName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.position)
            .ToList();

        return Ok(response);
    }

    private async Task<List<DashboardAppointmentRow>> BuildAppointmentsProjection(DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var appointments = await dbContext.Appointments
            .AsNoTracking()
            .Where(x => x.SlotTime >= start && x.SlotTime < end)
            .ToListAsync(cancellationToken);

        if (appointments.Count == 0)
        {
            return [];
        }

        var patientIds = appointments.Select(x => x.PatientId).Distinct().ToList();
        var doctorIds = appointments.Select(x => x.DoctorId).Distinct().ToList();
        var appointmentIds = appointments.Select(x => x.Id).Distinct().ToList();

        var patients = await dbContext.Patients
            .AsNoTracking()
            .Where(x => patientIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var doctors = await dbContext.Doctors
            .AsNoTracking()
            .Where(x => doctorIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        var queueEntries = await dbContext.QueueEntries
            .AsNoTracking()
            .Where(x => appointmentIds.Contains(x.AppointmentId))
            .ToListAsync(cancellationToken);

        var patientMap = patients.ToDictionary(x => x.Id, x => x);
        var doctorMap = doctors.ToDictionary(x => x.Id, x => x);
        var queueMap = queueEntries.ToDictionary(x => x.AppointmentId, x => x);

        return appointments
            .Select(appointment =>
            {
                patientMap.TryGetValue(appointment.PatientId, out var patient);
                doctorMap.TryGetValue(appointment.DoctorId, out var doctor);
                queueMap.TryGetValue(appointment.Id, out var queueEntry);

                return new DashboardAppointmentRow(
                    appointment.Id,
                    appointment.SlotTime,
                    patient?.Name.Value ?? "Unknown",
                    patient?.PhoneNumber.Value ?? string.Empty,
                    doctor?.Name.Value ?? appointment.DoctorId,
                    doctor?.Specialty ?? string.Empty,
                    queueEntry?.Position ?? appointment.QueuePosition ?? 0,
                    MapStatus(appointment, queueEntry));
            })
            .OrderBy(x => x.SlotTime)
            .ToList();
    }

    private static DateTime GetBatchStart(DateTime slotTime)
    {
        return new DateTime(
            slotTime.Year,
            slotTime.Month,
            slotTime.Day,
            slotTime.Hour,
            (slotTime.Minute / 30) * 30,
            0,
            slotTime.Kind);
    }

    private static string MapStatus(Appointment appointment, QueueEntry? queueEntry)
    {
        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            return "NO_SHOW";
        }

        if (appointment.Status == AppointmentStatus.Completed)
        {
            return "COMPLETED";
        }

        if (queueEntry is null)
        {
            return "BOOKED";
        }

        return queueEntry.Status switch
        {
            QueueStatus.InProgress => "IN_CONSULTATION",
            QueueStatus.Waiting => "IN_QUEUE",
            QueueStatus.Called => "IN_QUEUE",
            QueueStatus.Done => "COMPLETED",
            QueueStatus.Skipped => "NO_SHOW",
            _ => "BOOKED"
        };
    }

    private sealed record DashboardAppointmentRow(
        string AppointmentId,
        DateTime SlotTime,
        string PatientName,
        string PhoneNumber,
        string DoctorName,
        string Specialty,
        int QueuePosition,
        string Status);
}
