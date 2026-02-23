using ClinicQueue.Data;
using ClinicQueue.Shared.Models;
using Dapper;

namespace ClinicQueue.Services
{
    public class NotificationCronJobs
    {
        private readonly DatabaseService _db;
        private readonly IMetaWhatsAppService _whatsapp;
        private readonly IPatientService _patientService;

        public NotificationCronJobs(
            DatabaseService db,
            IMetaWhatsAppService whatsapp,
            IPatientService patientService)
        {
            _db = db;
            _whatsapp = whatsapp;
            _patientService = patientService;
        }

        public Task SendAppointmentReminders()
        {
            // Redundant: Replaced by Hangfire scheduling in ReminderService
            return Task.CompletedTask;
        }
    }
}
