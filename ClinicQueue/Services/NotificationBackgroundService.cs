using ClinicQueue.Data;
using ClinicQueue.Services;
using Dapper;

namespace ClinicQueue.Services
{
    public class NotificationBackgroundService
    {
        private readonly DatabaseService _db;
        private readonly IQueueService _queueService;
        private readonly IAppointmentService _appointmentService;
        private readonly IPatientService _patientService;
        private readonly IMetaWhatsAppService _whatsapp;
        private readonly ILogger<NotificationBackgroundService> _logger;

        public NotificationBackgroundService(
            DatabaseService db,
            IQueueService queueService,
            IAppointmentService appointmentService,
            IPatientService patientService,
            IMetaWhatsAppService whatsapp,
            ILogger<NotificationBackgroundService> logger)
        {
            _db = db;
            _queueService = queueService;
            _appointmentService = appointmentService;
            _patientService = patientService;
            _whatsapp = whatsapp;
            _logger = logger;
        }

        /// <summary>
        /// Notify patients when they reach position #2 in queue (next to be called)
        /// Runs every 1 minute
        /// </summary>
        public async Task NotifyNextInQueue()
        {
            try
            {
                _logger.LogInformation("Running NotifyNextInQueue job");

                using var connection = _db.GetConnection();

                // Get all patients at position #2
                var nextPatients = await connection.QueryAsync<dynamic>(@"
                    SELECT 
                        q.appointment_id as AppointmentId,
                        q.position as Position,
                        p.name as PatientName,
                        p.phone_number as PhoneNumber,
                        qph.next_in_queue_notified as AlreadyNotified
                    FROM queue q
                    JOIN appointments a ON q.appointment_id = a.id
                    JOIN patients p ON a.patient_id = p.id
                    LEFT JOIN queue_position_history qph ON qph.appointment_id = q.appointment_id
                    WHERE q.status = 'IN_QUEUE' 
                    AND q.position = 2
                ");

                foreach (var patient in nextPatients)
                {
                    // Skip if already notified
                    if (patient.AlreadyNotified == 1)
                    {
                        _logger.LogInformation($"Patient {patient.PatientName} already notified for position 2");
                        continue;
                    }

                    // Send "You're Next" notification
                    await _whatsapp.SendYouAreNextNotificationAsync(
                        patient.PhoneNumber,
                        patient.PatientName,
                        2,
                        10 // Estimated 10 minutes wait
                    );

                    // Mark as notified
                    await connection.ExecuteAsync(@"
                        INSERT INTO queue_position_history (appointment_id, last_notified_position, last_notification_time, next_in_queue_notified)
                        VALUES (@AppointmentId, 2, @Now, 1)
                        ON CONFLICT(appointment_id) DO UPDATE SET
                            next_in_queue_notified = 1,
                            last_notification_time = @Now
                    ", new { AppointmentId = patient.AppointmentId, Now = DateTime.UtcNow });

                    _logger.LogInformation($"Sent 'You're Next' notification to {patient.PatientName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in NotifyNextInQueue job");
            }
        }
    }
}
