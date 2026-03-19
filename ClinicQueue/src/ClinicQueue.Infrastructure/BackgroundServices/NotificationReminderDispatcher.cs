using ClinicQueue.Application.Interfaces;
using ClinicQueue.Domain.Enums;
using ClinicQueue.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ClinicQueue.Infrastructure.BackgroundServices;

public class NotificationReminderDispatcher(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<NotificationReminderDispatcher> logger) : BackgroundService
{
    private readonly int _reminderMinutesBefore = int.TryParse(configuration["ClinicSettings:ReminderMinutesBefore"], out var mins) && mins > 0
        ? mins
        : 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ClinicDbContext>();
                var whatsAppGateway = scope.ServiceProvider.GetRequiredService<IWhatsAppGateway>();

                var nowUtc = DateTime.UtcNow;
                var dueReminders = await dbContext.NotificationReminders
                    .Where(x => !x.IsCancelled && x.SentAt == null && x.Type == NotificationType.Reminder && x.ScheduledFor <= nowUtc)
                    .OrderBy(x => x.ScheduledFor)
                    .Take(25)
                    .ToListAsync(stoppingToken);

                foreach (var reminder in dueReminders)
                {
                    var appointment = await dbContext.Appointments.FirstOrDefaultAsync(x => x.Id == reminder.AppointmentId, stoppingToken);
                    if (appointment is null || appointment.Status == AppointmentStatus.Cancelled)
                    {
                        reminder.Cancel();
                        continue;
                    }

                    var patient = await dbContext.Patients.FirstOrDefaultAsync(x => x.Id == appointment.PatientId, stoppingToken);
                    var doctor = await dbContext.Doctors.FirstOrDefaultAsync(x => x.Id == appointment.DoctorId, stoppingToken);
                    if (patient is null)
                    {
                        reminder.Cancel();
                        continue;
                    }

                    var patientName = patient.Name.Value;
                    var doctorName = doctor?.Name.Value ?? "Doctor";
                    var message = $"Hi {patientName}! Your appointment with Dr. {doctorName} is in {_reminderMinutesBefore} minutes.\nPlease head to the clinic now. 🏥";

                    var sent = await whatsAppGateway.SendTextAsync(patient.PhoneNumber.Value, message, stoppingToken);
                    if (sent.IsSuccess)
                    {
                        reminder.MarkSent(nowUtc);
                    }
                    else
                    {
                        logger.LogWarning("Failed to send reminder for appointment {AppointmentId}: {Error}", reminder.AppointmentId, sent.Error);
                    }
                }

                if (dueReminders.Count > 0)
                {
                    await dbContext.SaveChangesAsync(stoppingToken);
                }

                logger.LogInformation("NotificationReminderDispatcher heartbeat at {UtcNow}", nowUtc);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NotificationReminderDispatcher cycle failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
