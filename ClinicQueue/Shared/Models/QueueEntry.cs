namespace ClinicQueue.Shared.Models
{
    public class QueueEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AppointmentId { get; set; } = string.Empty;
        public int Position { get; set; }
        public long PriorityScore { get; set; }
        public string Status { get; set; } = "IN_QUEUE";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
