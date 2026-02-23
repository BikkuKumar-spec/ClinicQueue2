using System;

namespace ClinicQueue.Shared.Models
{
    public class SymptomAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PatientId { get; set; } = string.Empty;
        public string? AppointmentId { get; set; }
        public string OriginalSymptoms { get; set; } = string.Empty;
        public string TranslatedSymptoms { get; set; } = string.Empty;
        public string RecommendedSpecialty { get; set; } = string.Empty;
        public string Severity { get; set; } = "Low"; // Low, Medium, High, Emergency
        public string AIReasoning { get; set; } = string.Empty;
        public string DetectedLanguage { get; set; } = "en";
        public string Intent { get; set; } = "Other"; // Greeting, Symptom, Menu, Other
        public string NormalizedEnglish { get; set; } = string.Empty;
        public double Confidence { get; set; } = 0.0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
