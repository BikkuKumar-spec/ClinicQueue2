namespace ClinicQueue.Shared.Models
{
    public class Appointment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PatientId { get; set; } = string.Empty;
        public string? PatientName { get; set; }
        public DateTime SlotTime { get; set; }
        public string Status { get; set; } = "BOOKED"; // BOOKED, ARRIVED, IN_QUEUE, IN_CONSULTATION, COMPLETED, NO_SHOW, CANCELLED
        public string DoctorName { get; set; } = string.Empty;
        public string Specialty { get; set; } = string.Empty;
        public int? QueuePosition { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation
        public Patient? Patient { get; set; }
    }
}
