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
            // Get or create patient
            var patient = await _patientService.GetOrCreateByPhoneAsync(request.PatientName, request.PhoneNumber);

            var appointment = new Appointment
            {
                Id = Guid.NewGuid().ToString(),
                PatientId = patient.Id,
                PatientName = request.PatientName.ToProperCase(),
                SlotTime = request.SlotTime,
                DoctorName = string.IsNullOrWhiteSpace(request.DoctorName) ? "General Physician" : request.DoctorName,
                Specialty = request.Specialty ?? string.Empty,
                Status = "BOOKED"
            };

            using var connection = _db.GetConnection();
            using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.Serializable);
            
            try
            {
                // 1. Resolve Doctor ID from Name
                var doctorId = await connection.ExecuteScalarAsync<int?>(@"
                    SELECT id FROM doctors WHERE name = @DoctorName AND is_active = 1 LIMIT 1",
                    new { DoctorName = appointment.DoctorName }, transaction);

                if (!doctorId.HasValue)
                {
                    throw new InvalidOperationException($"Doctor {appointment.DoctorName} not found or inactive.");
                }

                int dayOfWeek = (int)request.SlotTime.DayOfWeek;
                var slotTimeStr = request.SlotTime.ToString("HH:mm:ss");

                // 2. Lock the schedule and verify slot validity
                var schedule = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT start_time, end_time, slot_duration_minutes, max_patients_per_slot 
                    FROM schedules 
                    WHERE doctor_id = @DoctorId AND day_of_week = @DayOfWeek
                    FOR UPDATE",
                    new { DoctorId = doctorId.Value, DayOfWeek = dayOfWeek }, transaction);

                if (schedule == null)
                {
                    throw new InvalidOperationException($"Doctor {appointment.DoctorName} is not scheduled to work on this day.");
                }

                TimeSpan slotTimeTs = request.SlotTime.TimeOfDay;
                if (slotTimeTs < schedule.start_time || slotTimeTs >= schedule.end_time)
                {
                    throw new InvalidOperationException($"The requested time {request.SlotTime:hh:mm tt} is outside the working hours for {appointment.DoctorName}.");
                }

                // Verify the requested time aligns with the duration blocks (e.g. 00, 30)
                var minutesSinceStart = (slotTimeTs - (TimeSpan)schedule.start_time).TotalMinutes;
                if (minutesSinceStart % schedule.slot_duration_minutes != 0)
                {
                    throw new InvalidOperationException($"The requested time {request.SlotTime:hh:mm tt} is not a valid predefined slot interval.");
                }

                // 3. Count existing active bookings for this exact slot
                var currentBookings = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM appointments 
                    WHERE doctor_name = @DoctorName AND slot_time = @SlotTime 
                    AND status NOT IN ('CANCELLED', 'NO_SHOW')",
                    new { DoctorName = appointment.DoctorName, SlotTime = appointment.SlotTime }, transaction);

                if (currentBookings >= schedule.max_patients_per_slot)
                {
                    throw new InvalidOperationException($"Sorry, the slot at {appointment.SlotTime:hh:mm tt} is fully booked. Please select another time.");
                }

                // 4. Check if patient already has a booking at the exact same time
                var duplicatePatient = await connection.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM appointments 
                    WHERE patient_id = @PatientId AND slot_time = @SlotTime AND status NOT IN ('CANCELLED', 'NO_SHOW')",
                    new { PatientId = patient.Id, SlotTime = appointment.SlotTime }, transaction);

                if (duplicatePatient > 0)
                {
                    throw new InvalidOperationException($"Patient already has an active appointment exactly at {appointment.SlotTime:hh:mm tt}.");
                }

                await connection.ExecuteAsync(@"
                    INSERT INTO appointments (id, patient_id, patient_name, slot_time, status, doctor_name, specialty, created_at)
                    VALUES (@Id, @PatientId, @PatientName, @SlotTime, @Status, @DoctorName, @Specialty, UTC_TIMESTAMP())",
                    appointment, transaction);

                transaction.Commit();
                
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
            var now = DateTime.Now;
            var slots = new List<SlotDto>();
            using var connection = _db.GetConnection();

            var doctorId = await connection.ExecuteScalarAsync<int?>(
                "SELECT id FROM doctors WHERE name = @DoctorName AND is_active = 1 LIMIT 1",
                new { DoctorName = doctorName }
            );

            if (!doctorId.HasValue) return slots;

            int dayOfWeek = (int)targetDate.DayOfWeek;

            var schedule = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT start_time, end_time, slot_duration_minutes, max_patients_per_slot 
                FROM schedules 
                WHERE doctor_id = @DoctorId AND day_of_week = @DayOfWeek",
                new { DoctorId = doctorId.Value, DayOfWeek = dayOfWeek }
            );

            if (schedule == null) return slots; // Doctor does not work on this day

            // Fetch actual active bookings for the doctor exactly on that day
            var bookingsInfo = await connection.QueryAsync<DateTime>(@"
                SELECT slot_time FROM appointments 
                WHERE doctor_name = @DoctorName 
                AND DATE(slot_time) = DATE(@TargetDate) 
                AND status NOT IN ('CANCELLED', 'NO_SHOW')",
                new { DoctorName = doctorName, TargetDate = targetDate.Date }
            );

            var bookingCountsBySlot = bookingsInfo
                .GroupBy(b => b)
                .ToDictionary(g => g.Key, g => g.Count());

            var currentSlotTs = (TimeSpan)schedule.start_time;
            var endTimeTs = (TimeSpan)schedule.end_time;
            var durationMins = (int)schedule.slot_duration_minutes;
            var maxPatients = (int)schedule.max_patients_per_slot;

            while (currentSlotTs < endTimeTs)
            {
                var slotDateTime = targetDate.Date.Add(currentSlotTs);
                
                // Allow slots at least 5 mins in the future
                if (slotDateTime > now.AddMinutes(5))
                {
                    int currentBookings = bookingCountsBySlot.TryGetValue(slotDateTime, out var count) ? count : 0;
                    
                    slots.Add(new SlotDto
                    {
                        Time = slotDateTime,
                        BatchStartTime = slotDateTime,
                        BookingsInBatch = currentBookings,
                        IsAvailable = currentBookings < maxPatients,
                        MaxBookingsPerBatch = maxPatients
                    });
                }

                currentSlotTs = currentSlotTs.Add(TimeSpan.FromMinutes(durationMins));
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
                WHERE DATE(a.slot_time) = DATE(@TargetDate)
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
            
            // To group properly, we get the earliest and latest slot, or default 9-6 if none
            var startTime = targetDate.AddHours(9);
            var endTime = targetDate.AddHours(18);

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
