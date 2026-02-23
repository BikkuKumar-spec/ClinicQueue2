namespace ClinicQueue.Shared.DTOs
{
    public class CreateAppointmentRequest
    {
        public string PatientId { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public DateTime SlotTime { get; set; }
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
    }

    public class UpdateStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public class SlotDto
    {
        public DateTime Time { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime BatchStartTime { get; set; } // 30-min batch start
        public int BookingsInBatch { get; set; }      // Count of bookings in this batch
        public int MaxBookingsPerBatch { get; set; } = 5; // Max capacity
    }

    public class QueuePositionDto
    {
        public int Position { get; set; }
        public int EstimatedWaitMinutes { get; set; }
    }

    public class AppointmentWithPatientDto
    {
        public string Id { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime SlotTime { get; set; }
        public string Status { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public int? QueuePosition { get; set; }
        public string? SymptomSummary { get; set; }
        public string? SymptomSeverity { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AddToQueueRequest
    {
        public string AppointmentId { get; set; } = string.Empty;
    }

    public class DashboardSlotDto
    {
        public DateTime Time { get; set; }
        public bool IsBooked { get; set; }
        public int BookingsInBatch { get; set; }
        public int MaxBookingsPerBatch { get; set; } = 5;
        public List<PatientInBatchDto> Patients { get; set; } = new();
    }

    public class PatientInBatchDto
    {
        public string AppointmentId { get; set; } = string.Empty;
        public string PatientName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public int QueuePosition { get; set; }
        public string? SymptomSummary { get; set; }
        public string? SymptomSeverity { get; set; }
    }
}
