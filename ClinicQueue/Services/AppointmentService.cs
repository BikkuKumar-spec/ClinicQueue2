using ClinicQueue.Data;
using ClinicQueue.Shared.Models;
using ClinicQueue.Shared.DTOs;
using ClinicQueue.Hubs;
using Microsoft.AspNetCore.SignalR;
using Dapper;
using ClinicQueue.Shared.Utilities;

namespace ClinicQueue.Services
{
    public interface IAppointmentService
    {
        Task<Appointment> CreateAsync(CreateAppointmentRequest request);
        Task<Appointment?> GetByIdAsync(string id);
        Task<List<SlotDto>> GetAvailableSlotsAsync(DateTime targetDate, string doctorName);
        Task UpdateStatusAsync(string id, string status);
        Task<List<AppointmentWithPatientDto>> GetTodayAppointmentsAsync();
        Task<List<DashboardSlotDto>> GetDashboardSlotsAsync(DateTime? date = null, string? specialty = null, string? doctorName = null);
        Task<Appointment?> GetLastActiveAppointmentByPhoneAsync(string phoneNumber);
        Task RescheduleAsync(string id, DateTime newSlotTime);
    }

    public class AppointmentService : IAppointmentService
    {
        private readonly DatabaseService _db;
        private readonly IPatientService _patientService;
        private readonly IMetaWhatsAppService _whatsapp;
        private readonly IHubContext<DashboardHub> _hubContext;
        private readonly IReminderService _reminderService;
        private readonly IQueueService _queueService;

        public AppointmentService(
            DatabaseService db, 
            IPatientService patientService,
            IMetaWhatsAppService whatsapp,
            IHubContext<DashboardHub> hubContext,
            IReminderService reminderService,
            IQueueService queueService)
        {
            _db = db;
            _patientService = patientService;
            _whatsapp = whatsapp;
            _hubContext = hubContext;
            _reminderService = reminderService;
            _queueService = queueService;
        }

        public async Task<Appointment> CreateAsync(CreateAppointmentRequest request)
        {
            // Get or create patient (shared phone allowed, distinct records created/retrieved by service)
            var patient = await _patientService.GetOrCreateByPhoneAsync(request.PatientName, request.PhoneNumber);

            var appointment = new Appointment
            {
                PatientId = patient.Id,
                PatientName = request.PatientName.ToProperCase(),
                SlotTime = request.SlotTime,
                DoctorName = string.IsNullOrWhiteSpace(request.DoctorName) ? "General Physician" : request.DoctorName,
                Specialty = request.Specialty ?? string.Empty,
                Status = "BOOKED"
            };

            using var connection = _db.GetConnection();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                // Check batch capacity (max 5 per 30-minute batch)
                var batchStart = new DateTime(
                    request.SlotTime.Year,
                    request.SlotTime.Month,
                    request.SlotTime.Day,
                    request.SlotTime.Hour,
                    (request.SlotTime.Minute / 30) * 30, // Round down to nearest 30 min
                    0
                );
                var batchEnd = batchStart.AddMinutes(30);
                
                var bookingsInBatch = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM appointments 
                    WHERE strftime('%Y-%m-%d %H:%M', slot_time) >= strftime('%Y-%m-%d %H:%M', @BatchStart)
                    AND strftime('%Y-%m-%d %H:%M', slot_time) < strftime('%Y-%m-%d %H:%M', @BatchEnd)
                    AND doctor_name = @DoctorName
                    AND status != 'CANCELLED' AND status != 'NO_SHOW'",
                    new { BatchStart = batchStart, BatchEnd = batchEnd, DoctorName = request.DoctorName },
                    transaction
                );

                if (bookingsInBatch >= 5)
                {
                    throw new InvalidOperationException($"Sorry, the {batchStart:hh:mm tt}-{batchEnd:hh:mm tt} batch is full. Please select another time slot.");
                }
                
                await connection.ExecuteAsync(@"
                    INSERT INTO appointments (id, patient_id, patient_name, slot_time, status, doctor_name, specialty, created_at, updated_at)
                    VALUES (@Id, @PatientId, @PatientName, @SlotTime, @Status, @DoctorName, @Specialty, @CreatedAt, @UpdatedAt)",
                    appointment,
                    transaction
                );

                transaction.Commit();
                
                // Post-commit actions
                // Link symptom analysis if it exists in the session (handled by caller passing request.SymptomAnalysisId if needed, but for now we follow the session)
                await LinkSymptomAnalysisAsync(appointment.Id, patient.Id);
                
                await _reminderService.ScheduleReminderAsync(appointment.Id, appointment.SlotTime);
                await _hubContext.Clients.All.SendAsync("SlotBooked");
                
                return appointment;
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task RescheduleAsync(string id, DateTime newSlotTime)
        {
            using var connection = _db.GetConnection();
            await connection.ExecuteAsync(@"
                UPDATE appointments 
                SET slot_time = @SlotTime, 
                    status = 'BOOKED', 
                    reminder_sent = 0 
                WHERE id = @Id",
                new { Id = id, SlotTime = newSlotTime }
            );
        }

        public async Task<Appointment?> GetLastActiveAppointmentByPhoneAsync(string phoneNumber)
        {
            using var connection = _db.GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Appointment>(@"
                SELECT a.* FROM appointments a
                JOIN patients p ON a.patient_id = p.id
                WHERE p.phone_number = @Phone 
                AND a.status IN ('BOOKED', 'ARRIVED', 'IN_QUEUE')
                AND a.slot_time >= @Now
                ORDER BY a.slot_time ASC LIMIT 1",
                new { Phone = phoneNumber, Now = DateTime.Now.AddHours(-1) }
            );
        }

        public async Task<Appointment?> GetByIdAsync(string id)
        {
            using var connection = _db.GetConnection();
            return await connection.QueryFirstOrDefaultAsync<Appointment>(
                "SELECT * FROM appointments WHERE id = @Id",
                new { Id = id }
            );
        }

        public async Task<List<SlotDto>> GetAvailableSlotsAsync(DateTime targetDate, string doctorName)
        {
            var now = DateTime.Now; // Server time
            
            // Clinic Hours: 9 AM to 6 PM
            var startTime = targetDate.Date.AddHours(9);
            var endTime = targetDate.Date.AddHours(18);

            var slots = new List<SlotDto>();
            var currentBatch = startTime;

            using var connection = _db.GetConnection();
            
            while (currentBatch < endTime)
            {
                // BLOCK PAST SLOTS (at least 5 mins in future)
                bool isFuture = currentBatch > now.AddMinutes(5);
                bool shouldInclude = targetDate > now.Date || isFuture;
                
                if (shouldInclude)
                {
                    var bookingsInBatch = await connection.ExecuteScalarAsync<int>(@"
                        SELECT COUNT(*) FROM appointments 
                        WHERE strftime('%Y-%m-%d %H:%M', slot_time) >= strftime('%Y-%m-%d %H:%M', @BatchStart)
                        AND strftime('%Y-%m-%d %H:%M', slot_time) < strftime('%Y-%m-%d %H:%M', @BatchEnd)
                        AND doctor_name = @DoctorName
                        AND status != 'CANCELLED' AND status != 'NO_SHOW'",
                        new { BatchStart = currentBatch, BatchEnd = currentBatch.AddMinutes(30), DoctorName = doctorName }
                    );

                    slots.Add(new SlotDto
                    {
                        Time = currentBatch,
                        BatchStartTime = currentBatch,
                        BookingsInBatch = bookingsInBatch,
                        IsAvailable = bookingsInBatch < 5,
                        MaxBookingsPerBatch = 5
                    });
                }

                currentBatch = currentBatch.AddMinutes(30);
            }

            return slots;
        }

        public async Task UpdateStatusAsync(string id, string status)
        {
            using var connection = _db.GetConnection();
            using var transaction = connection.BeginTransaction();
            
            try
            {
                var rows = await connection.ExecuteAsync(@"
                    UPDATE appointments 
                    SET status = @Status, updated_at = @UpdatedAt 
                    WHERE id = @Id",
                    new { Status = status, UpdatedAt = DateTime.UtcNow, Id = id },
                    transaction
                );

                if (rows == 0)
                {
                    throw new Exception($"Appointment with ID {id} not found.");
                }

                transaction.Commit();
            }
            catch (Exception)
            {
                try { transaction.Rollback(); } catch { }
                throw;
            }

            // Post-commit actions (Non-transactional or handles own transactions)
            try
            {
                // Join Queue on ARRIVED
                if (status == "ARRIVED")
                {
                    await _queueService.AddToQueueAsync(id);
                }
                // Leave Queue on IN_CONSULTATION, COMPLETED, or NO_SHOW
                else if (status == "IN_CONSULTATION" || status == "COMPLETED" || status == "NO_SHOW" || status == "CANCELLED")
                {
                    await _queueService.RemoveFromQueueAsync(id);
                    
                    if (status == "COMPLETED")
                    {
                        var apt = await GetByIdAsync(id);
                        if (apt != null)
                        {
                            var patient = await _patientService.GetByIdAsync(apt.PatientId);
                            if (patient != null)
                            {
                                await _whatsapp.SendTextMessageAsync(patient.PhoneNumber, 
                                    $"🙏 *Thank you for visiting!*\n\nWe hope you have a great day. Feel free to book again anytime.\n\n_ClinicQueue_");
                            }
                        }
                    }
                }

                await _hubContext.Clients.All.SendAsync("StatusChanged");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the primary status update which already committed
                Console.WriteLine($"[ERROR] Post-status update action failed for {id}: {ex.Message}");
            }
        }

        public async Task<List<AppointmentWithPatientDto>> GetTodayAppointmentsAsync()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            using var connection = _db.GetConnection();
            var appointments = await connection.QueryAsync<AppointmentWithPatientDto>(@"
                SELECT 
                    a.id,
                    a.slot_time as SlotTime,
                    a.status,
                    a.doctor_name as DoctorName,
                    a.specialty as Specialty,
                    a.queue_position as QueuePosition,
                    COALESCE(a.patient_name, p.name) as PatientName,
                    p.phone_number as PhoneNumber,
                    sa.original_symptoms as SymptomSummary,
                    sa.severity as SymptomSeverity
                FROM appointments a
                JOIN patients p ON a.patient_id = p.id
                LEFT JOIN symptom_analyses sa ON a.id = sa.appointment_id
                WHERE a.slot_time >= @Today AND a.slot_time < @Tomorrow
                ORDER BY a.slot_time ASC",
                new { Today = today, Tomorrow = tomorrow }
            );

            return appointments.ToList();
        }

        public async Task<List<DashboardSlotDto>> GetDashboardSlotsAsync(DateTime? date = null, string? specialty = null, string? doctorName = null)
        {
            var targetDate = date ?? DateTime.Today;
            var startTime = targetDate.AddHours(9);
            var endTime = targetDate.AddHours(18);

            using var connection = _db.GetConnection();
            
            var query = @"
                SELECT 
                    a.id, 
                    a.slot_time, 
                    a.status, 
                    a.doctor_name, 
                    a.specialty, 
                    a.queue_position,
                    a.patient_name as apt_pname,
                    p.name as p_name,
                    p.phone_number,
                    sa.original_symptoms as symptom_summary,
                    sa.severity as symptom_severity
                FROM appointments a
                JOIN patients p ON a.patient_id = p.id
                LEFT JOIN symptom_analyses sa ON a.id = sa.appointment_id
                WHERE date(a.slot_time) = date(@TargetDate)
                AND a.status != 'CANCELLED'";

            if (!string.IsNullOrEmpty(specialty))
            {
                query += " AND a.specialty = @Specialty";
            }
            if (!string.IsNullOrEmpty(doctorName))
            {
                query += " AND a.doctor_name = @DoctorName";
            }

            query += " ORDER BY a.slot_time";

            var appointments = await connection.QueryAsync<dynamic>(query,
                new { TargetDate = targetDate, Specialty = specialty, DoctorName = doctorName }
            );

            var result = new List<DashboardSlotDto>();
            var currentBatch = startTime;

            while (currentBatch < endTime)
            {
                var batchAppointments = appointments.Where(a => 
                {
                    DateTime slotTime;
                    if (a.slot_time is DateTime dt) slotTime = dt;
                    else if (a.slot_time is string str && DateTime.TryParse(str, out slotTime)) { }
                    else return false;
                    
                    var bStart = new DateTime(slotTime.Year, slotTime.Month, slotTime.Day, slotTime.Hour, (slotTime.Minute / 30) * 30, 0);
                    return bStart == currentBatch;
                }).ToList();

                result.Add(new DashboardSlotDto
                {
                    Time = currentBatch,
                    IsBooked = batchAppointments.Any(),
                    BookingsInBatch = batchAppointments.Count,
                    MaxBookingsPerBatch = 5,
                    Patients = batchAppointments.Select(apt => 
                    {
                        var resolvedName = !string.IsNullOrEmpty((string?)apt.apt_pname) ? (string)apt.apt_pname : (string)apt.p_name;
                        return new PatientInBatchDto
                        {
                            AppointmentId = apt.id,
                            PatientName = resolvedName,
                            PhoneNumber = apt.phone_number,
                            Status = apt.status,
                            DoctorName = apt.doctor_name ?? "",
                            Specialty = apt.specialty ?? "",
                            QueuePosition = (int?)apt.queue_position ?? 0,
                            SymptomSummary = apt.symptom_summary,
                            SymptomSeverity = apt.symptom_severity
                        };
                    }).ToList()
                });

                currentBatch = currentBatch.AddMinutes(30);
            }

            return result;
        }

        private async Task LinkSymptomAnalysisAsync(string appointmentId, string patientId)
        {
            using var connection = _db.GetConnection();
            // Find the most recent unlinked symptom analysis for this patient
            var analysisId = await connection.QueryFirstOrDefaultAsync<string>(@"
                SELECT id FROM symptom_analyses 
                WHERE patient_id = @PatientId AND appointment_id IS NULL
                ORDER BY created_at DESC LIMIT 1",
                new { PatientId = patientId }
            );

            if (!string.IsNullOrEmpty(analysisId))
            {
                await connection.ExecuteAsync(@"
                    UPDATE symptom_analyses SET appointment_id = @AppointmentId 
                    WHERE id = @AnalysisId",
                    new { AppointmentId = appointmentId, AnalysisId = analysisId }
                );
            }
        }
    }
}
