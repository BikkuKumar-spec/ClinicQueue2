using ClinicQueue.Shared.Models;

namespace ClinicQueue.Shared.Models
{
    public class Patient
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastVisit { get; set; }
    }
}
