using ClinicQueue.Data;
using ClinicQueue.Shared.Models;
using ClinicQueue.Shared.DTOs;
using ClinicQueue.Hubs;
using Microsoft.AspNetCore.SignalR;
using Dapper;

namespace ClinicQueue.Services
{
    public interface IQueueService
    {
        Task AddToQueueAsync(string appointmentId);
        Task RemoveFromQueueAsync(string appointmentId);
        Task<List<QueueEntry>> GetCurrentQueueAsync(string? doctorName = null);
        Task<List<object>> GetCurrentQueueWithPatientsAsync(string? doctorName = null);
        Task<QueuePositionDto> GetPositionAsync(string appointmentId);
        Task ReorderQueueAsync(string? doctorName = null);
    }

    public class QueueService : IQueueService
    {
        private readonly DatabaseService _db;
        private readonly IMetaWhatsAppService _whatsapp;
        private readonly IPatientService _patientService;
        private readonly IHubContext<DashboardHub> _hubContext;

        public QueueService(
            DatabaseService db, 
            IMetaWhatsAppService whatsapp,
            IPatientService patientService,
            IHubContext<DashboardHub> hubContext)
        {
            _db = db;
            _whatsapp = whatsapp;
            _patientService = patientService;
            _hubContext = hubContext;
        }

        public async Task AddToQueueAsync(string appointmentId)
        {
            using var connection = _db.GetConnection();
            
            // Get appointment details
            var appointment = await connection.QueryFirstOrDefaultAsync<Appointment>(
                "SELECT * FROM appointments WHERE id = @Id",
                new { Id = appointmentId }
            );

            if (appointment == null) return;

            // Calculate priority score (FIFO based on arrival)
            var priorityScore = CalculatePriority(appointment.SlotTime);

            // Add to queue (explicitly set all fields for Dapper)
            var now = DateTime.UtcNow;
            var queueEntry = new QueueEntry
            {
                Id = Guid.NewGuid().ToString(),
                AppointmentId = appointmentId,
                PriorityScore = priorityScore,
                Status = "IN_QUEUE",
                Position = 0, // Will be set by ReorderQueue
                CreatedAt = now,
                UpdatedAt = now
            };

            await connection.ExecuteAsync(@"
                INSERT OR IGNORE INTO queue (id, appointment_id, priority_score, status, position, created_at, updated_at)
                VALUES (@Id, @AppointmentId, @PriorityScore, @Status, @Position, @CreatedAt, @UpdatedAt)",
                queueEntry
            );

            // Update appointment status
            await connection.ExecuteAsync(@"
                UPDATE appointments SET status = 'IN_QUEUE' WHERE id = @Id",
                new { Id = appointmentId }
            );

            // Reorder queue for this doctor and notify
            await ReorderQueueAsync(appointment.DoctorName);
            
            // Get new position after reordering
            var entry = await connection.QueryFirstOrDefaultAsync<QueueEntry>(
                "SELECT * FROM queue WHERE appointment_id = @Id",
                new { Id = appointmentId }
            );
            
            if (entry != null)
            {
                // Send arrival confirmation (Notification 1 - Automation)
                var patient = await _patientService.GetByIdAsync(appointment.PatientId);
                if (patient != null)
                {
                    // Formula: (queuePosition - 1) * 10
                    var waitMinutes = (entry.Position - 1) * 10;
                    var patientName = appointment.PatientName ?? patient.Name;
                    var doctorName = appointment.DoctorName ?? "Dr. Sharma";
                    
                    // Send arrival confirmation with button
                    var welcomeText = $"✅ *{patient.Name}*, you are now checked in!\n\n👨‍⚕️ *Doctor:* {appointment.DoctorName}\n\nPlease stay nearby. We'll notify you when you're next.";
                    var buttons = new List<ButtonDto>
                    {
                        new ButtonDto { Id = $"btn_queue_{appointmentId}", Title = "📍 View Position" }
                    };

                    await _whatsapp.SendButtonMessageAsync(patient.PhoneNumber, welcomeText, buttons);
                    
                    // Track this notification
                    await connection.ExecuteAsync(@"
                        INSERT OR REPLACE INTO queue_position_history (appointment_id, last_notified_position, last_notification_time)
                        VALUES (@AppointmentId, @Position, @Time)",
                        new { AppointmentId = appointmentId, Position = entry.Position, Time = DateTime.UtcNow }
                    );
                }
            }
            
            // Broadcast real-time update to dashboard
            await _hubContext.Clients.All.SendAsync("QueueUpdated");
        }

        public async Task RemoveFromQueueAsync(string appointmentId)
        {
            using var connection = _db.GetConnection();
            
            // Get doctor name before deleting to reorder correctly
            var doctorName = await connection.QueryFirstOrDefaultAsync<string>(@"
                SELECT doctor_name FROM appointments WHERE id = @Id",
                new { Id = appointmentId }
            );

            await connection.ExecuteAsync(
                "DELETE FROM queue WHERE appointment_id = @AppointmentId",
                new { AppointmentId = appointmentId }
            );

            // Reorder remaining queue for this doctor
            if (!string.IsNullOrEmpty(doctorName))
            {
                await ReorderQueueAsync(doctorName);
            }
            
            // Broadcast real-time update to dashboard
            await _hubContext.Clients.All.SendAsync("QueueUpdated");
        }

        public async Task<List<QueueEntry>> GetCurrentQueueAsync(string? doctorName = null)
        {
            using var connection = _db.GetConnection();
            var sql = @"
                SELECT q.* FROM queue q
                JOIN appointments a ON q.appointment_id = a.id
                WHERE q.status = 'IN_QUEUE'";
            
            if (!string.IsNullOrEmpty(doctorName))
            {
                sql += " AND a.doctor_name = @DoctorName";
            }
            
            sql += " ORDER BY q.priority_score ASC";

            var entries = await connection.QueryAsync<QueueEntry>(sql, new { DoctorName = doctorName });
            return entries.ToList();
        }

        public async Task<List<object>> GetCurrentQueueWithPatientsAsync(string? doctorName = null)
        {
            using var connection = _db.GetConnection();
            var sql = @"
                SELECT 
                    q.id,
                    q.appointment_id as AppointmentId,
                    q.position as Position,
                    q.priority_score as PriorityScore,
                    COALESCE(a.patient_name, p.name) as ResolvedPatientName,
                    p.phone_number as PhoneNumber,
                    a.slot_time as SlotTime
                FROM queue q
                JOIN appointments a ON q.appointment_id = a.id
                JOIN patients p ON a.patient_id = p.id
                WHERE q.status = 'IN_QUEUE'";

            if (!string.IsNullOrEmpty(doctorName))
            {
                sql += " AND a.doctor_name = @DoctorName";
            }

            sql += " ORDER BY q.priority_score ASC";

            var entries = await connection.QueryAsync<dynamic>(sql, new { DoctorName = doctorName });
            
            return entries.Select(e => new
            {
                Id = e.id,
                AppointmentId = e.AppointmentId,
                Position = e.Position,
                PriorityScore = e.PriorityScore,
                PatientName = e.ResolvedPatientName,
                PhoneNumber = e.PhoneNumber,
                SlotTime = e.SlotTime,
                EstimatedWait = ((int)e.Position - 1) * 10
            }).ToList<object>();
        }

        public async Task<QueuePositionDto> GetPositionAsync(string appointmentId)
        {
            using var connection = _db.GetConnection();
            var entry = await connection.QueryFirstOrDefaultAsync<QueueEntry>(
                "SELECT * FROM queue WHERE appointment_id = @AppointmentId",
                new { AppointmentId = appointmentId }
            );

            if (entry == null)
                return new QueuePositionDto { Position = 0, EstimatedWaitMinutes = 0 };

            return new QueuePositionDto
            {
                Position = entry.Position,
                EstimatedWaitMinutes = entry.Position * 10 // 10 min per patient (includes 1 in consult)
            };
        }

        public async Task ReorderQueueAsync(string? doctorName = null)
        {
            using var connection = _db.GetConnection();
            
            // If doctorName is provided, only reorder that doctor's queue
            // If null, we might want to reorder ALL doctors individually
            var doctorsToReorder = !string.IsNullOrEmpty(doctorName) 
                ? new List<string> { doctorName }
                : (await connection.QueryAsync<string>("SELECT DISTINCT doctor_name FROM appointments")).ToList();

            foreach (var doc in doctorsToReorder)
            {
                // Get queue entries for this doctor ordered by priority
                var entries = await connection.QueryAsync<QueueEntry>(@"
                    SELECT q.* FROM queue q
                    JOIN appointments a ON q.appointment_id = a.id
                    WHERE q.status = 'IN_QUEUE' AND a.doctor_name = @DoctorName
                    ORDER BY q.priority_score ASC",
                    new { DoctorName = doc }
                );

                int position = 1;
                foreach (var entry in entries)
                {
                    entry.Position = position;

                    // Update position in database
                    await connection.ExecuteAsync(@"
                        UPDATE queue 
                        SET position = @Position, updated_at = @UpdatedAt 
                        WHERE id = @Id",
                        new { Position = entry.Position, UpdatedAt = DateTime.UtcNow, Id = entry.Id }
                    );

                    // Update appointment
                    await connection.ExecuteAsync(@"
                        UPDATE appointments 
                        SET queue_position = @Position 
                        WHERE id = @AppointmentId",
                        new { Position = entry.Position, AppointmentId = entry.AppointmentId }
                    );

                    // Smart position-aware notifications
                    var lastNotified = await connection.QueryFirstOrDefaultAsync<int?>(@"
                        SELECT last_notified_position FROM queue_position_history 
                        WHERE appointment_id = @AppointmentId",
                        new { AppointmentId = entry.AppointmentId }
                    ) ?? 0;
                    
                    var appointment = await connection.QueryFirstOrDefaultAsync<Appointment>(
                        "SELECT * FROM appointments WHERE id = @Id",
                        new { Id = entry.AppointmentId }
                    );
                    var patient = await _patientService.GetByIdAsync(appointment!.PatientId);
                    
                    if (patient != null)
                    {
                        string? message = null;
                        int newNotifiedPosition = lastNotified;
                        var currentDoctorName = appointment!.DoctorName ?? "Dr. Sharma";
                        
                        // Notification 2: Position becomes #1 (Automation)
                        if (position == 1 && lastNotified != 1)
                        {
                            message = _whatsapp.GetNextInLineNotification(currentDoctorName);
                            newNotifiedPosition = 1;
                        }

                        if (message != null)
                        {
                            await _whatsapp.SendTextMessageAsync(patient.PhoneNumber, message);
                            
                            // Update notification tracking
                            await connection.ExecuteAsync(@"
                                INSERT OR REPLACE INTO queue_position_history (appointment_id, last_notified_position, last_notification_time)
                                VALUES (@AppointmentId, @Position, @Time)",
                                new { AppointmentId = entry.AppointmentId, Position = newNotifiedPosition, Time = DateTime.UtcNow }
                            );
                        }
                    }

                    position++;
                }
            }
        }

        private long CalculatePriority(DateTime slotTime)
        {
            // Strict FIFO based on Arrival Time (When marked 'Arrive')
            // Using Ticks ensures nanosecond precision for order
            return DateTime.UtcNow.Ticks;
        }
    }
}
