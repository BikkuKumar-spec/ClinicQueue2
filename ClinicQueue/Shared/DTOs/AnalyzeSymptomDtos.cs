using System.Collections.Generic;

namespace ClinicQueue.Shared.DTOs
{
    public class ChatMessage
    {
        public string Role { get; set; } = "user"; // "user", "assistant", "system"
        public string Content { get; set; } = string.Empty;
    }

    public class AnalyzeSymptomRequest
    {
        public string PatientId { get; set; } = string.Empty;
        public List<ChatMessage> History { get; set; } = new List<ChatMessage>();
    }

    public class AnalyzeSymptomResponse
    {
        public bool IsFinal { get; set; }
        public string? FollowUpQuestion { get; set; }
        public SymptomResult? Recommendation { get; set; }
    }

    public class SymptomResult
    {
        public string Specialty { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Severity { get; set; } = "Medium";
    }
}
