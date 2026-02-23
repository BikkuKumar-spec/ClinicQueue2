using ClinicQueue.Data;
using Dapper;

namespace ClinicQueue.Services
{
    public class DoctorService : IDoctorService
    {
        private readonly DatabaseService _db;
        private readonly IMetaWhatsAppService _whatsappService;
        private readonly ILogger<DoctorService> _logger;

        public DoctorService(
            DatabaseService db, 
            IMetaWhatsAppService whatsappService,
            ILogger<DoctorService> logger)
        {
            _db = db;
            _whatsappService = whatsappService;
            _logger = logger;
        }

        public async Task<int?> GetDoctorStatusAsync(string doctorId)
        {
            using var connection = _db.GetConnection();
            return await connection.ExecuteScalarAsync<int?>(
                "SELECT status FROM doctors WHERE id = @Id", new { Id = doctorId });
        }

        public async Task SetDoctorStatusAsync(string doctorId, int status)
        {
            using var connection = _db.GetConnection();
            
            // 1. Update Status in Database
            var exists = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM doctors WHERE id = @Id", new { Id = doctorId });

            var doctorName = doctorId; 
            if (exists > 0)
            {
               doctorName = await connection.ExecuteScalarAsync<string>(
                   "SELECT name FROM doctors WHERE id = @Id", new { Id = doctorId }) ?? doctorId;

               await connection.ExecuteAsync(
                    "UPDATE doctors SET status = @Status, updated_at = CURRENT_TIMESTAMP WHERE id = @Id",
                    new { Id = doctorId, Status = status });
            }
            else
            {
                await connection.ExecuteAsync(
                    "INSERT INTO doctors (id, name, status) VALUES (@Id, @Id, @Status)",
                    new { Id = doctorId, Status = status });
            }

            // 2. Broadcast Logic - Filter Trigger
            // Status: 0 = Available, 1 = Consulting, 2 = On Break, 3 = Offline
            // Requirement: Only broadcast for AVAILABLE (0) or ON BREAK (2)
            if (status == 0 || status == 2)
            {
                _ = BroadcastStatusUpdateAsync(doctorId, doctorName, status);
            }
        }

        private async Task BroadcastStatusUpdateAsync(string doctorId, string doctorName, int status)
        {
            try
            {
                using var connection = _db.GetConnection();

                // 3. Fetch Audience
                // Filter: Doctor matches AND Status is 'BOOKED' or 'ARRIVED' (Queued)
                // We use Dapper, so it is inherently "NoTracking" (disconnected data)
                var patients = await connection.QueryAsync<PatientNotificationDto>(@"
                    SELECT 
                        p.phone_number as PhoneNumber,
                        p.name as PatientName
                    FROM appointments a
                    JOIN patients p ON a.patient_id = p.id
                    WHERE (a.doctor_name = @DoctorId OR a.doctor_name = @DoctorName)
                    AND a.status IN ('BOOKED', 'ARRIVED')
                    AND a.slot_time >= DATE('now')
                ", new { DoctorId = doctorId, DoctorName = doctorName });

                // 4. Message Content
                string message = status == 2 
                    ? $"⚠️ Status Update: {doctorName} is on a short break. The queue is currently paused. We will notify you when it resumes."
                    : $"✅ Status Update: {doctorName} is now Available and the queue is moving again. Please be ready!";

                // 5. Execution - Loop and Send
                foreach (var patient in patients)
                {
                    if (!string.IsNullOrEmpty(patient.PhoneNumber))
                    {
                        try 
                        {
                            await _whatsappService.SendTextMessageAsync(patient.PhoneNumber, message);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Failed to send WhatsApp to {patient.PhoneNumber}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex) 
            {
                _logger.LogError($"Error in broadcast background task: {ex.Message}");
            }
        }

        private class PatientNotificationDto
        {
            public string PhoneNumber { get; set; } = "";
            public string PatientName { get; set; } = "";
        }
    }
}
