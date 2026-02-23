namespace ClinicQueue.Shared.Models
{
    public class NotificationLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string? AppointmentId { get; set; }
        public string Phone { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Template { get; set; }
        public string Status { get; set; } = "pending";
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}
