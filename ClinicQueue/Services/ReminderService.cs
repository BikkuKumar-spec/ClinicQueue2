using Hangfire;
using Dapper;
using ClinicQueue.Data;
using ClinicQueue.Shared.DTOs;
using ClinicQueue.Shared.Utilities;

namespace ClinicQueue.Services
{
    public interface IReminderService
    {
        Task SendAppointmentReminderAsync(string appointmentId);
        Task ScheduleReminderAsync(string appointmentId, DateTime slotTime);
        Task CancelReminderAsync(string appointmentId);
        Task HandleReminderYesAsync(string appointmentId, string phoneNumber);
        Task HandleReminderNoAsync(string appointmentId, string phoneNumber);
    }

    public class ReminderService : IReminderService
    {
        private class ReminderInfo
        {
            public string Id { get; set; } = "";
            public string Status { get; set; } = "";
            public DateTime SlotTime { get; set; }
            public string ResolvedPatientName { get; set; } = "";
            public int ReminderSent { get; set; }
            public string PhoneNumber { get; set; } = "";
        }

        private readonly IMetaWhatsAppService _metaService;
        private readonly DatabaseService _db;
        private readonly ILogger<ReminderService> _logger;
        private readonly ITranslationService _translator;
        private readonly BotSessionStore _sessionStore;

        public ReminderService(
            IMetaWhatsAppService metaService,
            DatabaseService db,
            ILogger<ReminderService> logger,
            ITranslationService translator,
            BotSessionStore sessionStore)
        {
            _metaService = metaService;
            _db = db;
            _logger = logger;
            _translator = translator;
            _sessionStore = sessionStore;
        }

        // ─── Language Helper ──────────────────────────────────────────────────────

        /// <summary>
        /// Gets the stored language for a phone number from BotSessionStore.
        /// Falls back to English if not set.
        /// </summary>
        private string GetUserLanguage(string phoneNumber)
            => _sessionStore.GetLanguage(phoneNumber);

        /// <summary>
        /// Translates English text to the user's language.
        /// Returns original text if translation fails or language is English.
        /// </summary>
        private async Task<string> T(string englishText, string lang)
        {
            if (string.IsNullOrEmpty(lang) || lang == "eng_Latn")
                return englishText;
            try
            {
                var result = await _translator.FromEnglishAsync(englishText, lang);
                return string.IsNullOrWhiteSpace(result) ? englishText : result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"[ReminderService] Translation failed lang={lang}: {ex.Message}");
                return englishText;
            }
        }

        // ─── Schedule / Cancel ────────────────────────────────────────────────────

        public Task ScheduleReminderAsync(string appointmentId, DateTime slotTime)
        {
            if (slotTime.Kind == DateTimeKind.Unspecified)
                slotTime = DateTime.SpecifyKind(slotTime, DateTimeKind.Local);

            var reminderTime = slotTime.AddMinutes(-10);

            if (reminderTime > DateTime.Now)
            {
                BackgroundJob.Schedule<IReminderService>(
                    x => x.SendAppointmentReminderAsync(appointmentId),
                    reminderTime.ToUniversalTime()
                );
                _logger.LogInformation($"Scheduled reminder for appointment {appointmentId} at {reminderTime} (Local) / {reminderTime.ToUniversalTime()} (UTC)");
            }
            else
            {
                _logger.LogWarning($"Cannot schedule reminder for {appointmentId} - time has passed");
            }

            return Task.CompletedTask;
        }

        public Task CancelReminderAsync(string appointmentId)
        {
            _logger.LogInformation($"Reminder cancellation requested for {appointmentId}");
            return Task.CompletedTask;
        }

        // ─── Send Reminder ────────────────────────────────────────────────────────

        [AutomaticRetry(Attempts = 0)]
        public async Task SendAppointmentReminderAsync(string appointmentId)
        {
            try
            {
                _logger.LogInformation($"Processing reminder for appointment {appointmentId}");

                using var connection = _db.GetConnection();

                var appointment = await connection.QueryFirstOrDefaultAsync<ReminderInfo>(@"
                    SELECT 
                        a.id as Id,
                        a.status as Status,
                        a.slot_time as SlotTime,
                        COALESCE(a.patient_name, p.name) as ResolvedPatientName,
                        a.reminder_sent as ReminderSent,
                        p.phone_number as PhoneNumber
                    FROM appointments a
                    JOIN patients p ON a.patient_id = p.id
                    WHERE a.id = @AppointmentId
                ", new { AppointmentId = appointmentId });

                if (appointment == null)
                {
                    _logger.LogWarning($"Appointment {appointmentId} not found");
                    return;
                }

                if (appointment.Status != "BOOKED")
                {
                    _logger.LogInformation($"Skipping reminder for {appointmentId} - status is {appointment.Status}");
                    return;
                }

                if (appointment.ReminderSent == 1)
                {
                    _logger.LogInformation($"Skipping reminder for {appointmentId} - already sent");
                    return;
                }

                // ✅ Get user's stored language from BotSessionStore
                var lang = GetUserLanguage(appointment.PhoneNumber);
                _logger.LogInformation($"[ReminderService] Sending reminder in lang='{lang}' to {appointment.PhoneNumber}");

                string dateDisplay = appointment.SlotTime.ToString("dddd, MMM dd, yyyy");
                string timeDisplay = appointment.SlotTime.ToString("h:mm tt");

                var title   = await T("⏰ *Appointment Reminder*", lang);
                var hi      = await T($"Hi {appointment.ResolvedPatientName.ToProperCase()},", lang);
                var in10Min = await T("Your appointment is in *10 minutes*.", lang);
                var onWay   = await T("Are you on your way?", lang);

                string message = $"{title}\n\n" +
                                 $"{hi}\n" +
                                 $"{in10Min}\n\n" +
                                 $"📅 {dateDisplay}\n" +
                                 $"⏰ {timeDisplay}\n" +
                                 $"👨‍⚕️ Dr. Sharma's Clinic\n\n" +
                                 $"{onWay}";

                var yesTitle = await T("✅ Yes, I'm coming", lang);
                var noTitle  = await T("❌ No, I can't come", lang);

                await _metaService.SendButtonMessageAsync(
                    appointment.PhoneNumber,
                    message,
                    new List<ButtonDto>
                    {
                        new ButtonDto { Id = $"reminder_yes_{appointmentId}", Title = yesTitle },
                        new ButtonDto { Id = $"reminder_no_{appointmentId}", Title = noTitle }
                    }
                );

                await connection.ExecuteAsync(@"
                    UPDATE appointments 
                    SET reminder_sent = 1 
                    WHERE id = @AppointmentId
                ", new { AppointmentId = appointmentId });

                _logger.LogInformation($"Reminder sent successfully to {appointment.PhoneNumber} for appointment {appointmentId}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending reminder for {appointmentId}: {ex.Message}");
                throw;
            }
        }

        // ─── Handle Reminder Responses ────────────────────────────────────────────

        public async Task HandleReminderYesAsync(string appointmentId, string phoneNumber)
        {
            using var connection = _db.GetConnection();

            await connection.ExecuteAsync(@"
                UPDATE appointments 
                SET expected_to_arrive = 1 
                WHERE id = @AppointmentId
            ", new { AppointmentId = appointmentId });

            // ✅ Use user's stored language
            var lang = GetUserLanguage(phoneNumber);

            var msgRaw = "👍 *Great!*\n\n" +
                         "Please proceed to the clinic.\n" +
                         "We'll update your queue position once you arrive.\n\n" +
                         "_ClinicQueue_";

            var msg = await T(msgRaw, lang);
            await _metaService.SendTextMessageAsync(phoneNumber, msg);

            _logger.LogInformation($"Appointment {appointmentId} marked as expected to arrive");
        }

        public async Task HandleReminderNoAsync(string appointmentId, string phoneNumber)
        {
            using var connection = _db.GetConnection();

            await connection.ExecuteAsync(@"
                UPDATE appointments 
                SET status = 'NO_SHOW', updated_at = CURRENT_TIMESTAMP 
                WHERE id = @AppointmentId
            ", new { AppointmentId = appointmentId });

            // ✅ Use user's stored language
            var lang = GetUserLanguage(phoneNumber);

            var cancelMsgRaw = "❌ *Appointment Cancelled*\n\n" +
                               "Thanks for letting us know.\n" +
                               "If you want to reschedule your appointment, please click below:";

            var cancelMsg = await T(cancelMsgRaw, lang);
            var btnTitle  = await T("📅 Reschedule Now", lang);

            await _metaService.SendButtonMessageAsync(
                phoneNumber,
                cancelMsg,
                new List<ButtonDto>
                {
                    new ButtonDto { Id = "btn_book", Title = btnTitle }
                }
            );

            _logger.LogInformation($"Appointment {appointmentId} marked as NO_SHOW due to reminder response. Rescheduling offered.");
        }
    }
}