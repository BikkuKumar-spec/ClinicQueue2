using System.Collections.Concurrent;

namespace ClinicQueue.Infrastructure.ExternalServices.WhatsApp;

public sealed class WhatsAppConversationSessionStore
{
    private readonly ConcurrentDictionary<string, (string State, BookingSession Data)> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _userLanguages = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public bool TryGet(string phoneNumber, out (string State, BookingSession Data) session)
    {
        return _sessions.TryGetValue(phoneNumber, out session);
    }

    public void Set(string phoneNumber, string state, BookingSession data)
    {
        _sessions[phoneNumber] = (state, data);
    }

    public bool Remove(string phoneNumber)
    {
        return _sessions.TryRemove(phoneNumber, out _);
    }

    public SemaphoreSlim GetLock(string phoneNumber)
    {
        return _locks.GetOrAdd(phoneNumber, static _ => new SemaphoreSlim(1, 1));
    }

    public string GetLanguage(string phoneNumber)
    {
        return _userLanguages.TryGetValue(phoneNumber, out var lang) ? lang : "eng_Latn";
    }

    public void SetLanguage(string phoneNumber, string language)
    {
        _userLanguages[phoneNumber] = language;
    }

    public bool IsEnglish(string phoneNumber)
    {
        var lang = GetLanguage(phoneNumber);
        return string.IsNullOrEmpty(lang) || string.Equals(lang, "eng_Latn", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class BookingSession
{
    public bool HasGreeted { get; set; }
    public string? Step { get; set; }
    public string? KnownPatientName { get; set; }
    public bool IsBookingForSomeoneElse { get; set; }
    public string? PatientName { get; set; }
    public string? PatientNameEnglish { get; set; }
    public string? Specialty { get; set; }
    public string? DoctorName { get; set; }
    public string? DoctorId { get; set; }
    public string? RescheduleAppointmentId { get; set; }
    public DateTime? SelectedDate { get; set; }
    public DateTime? SelectedTime { get; set; }
    public string? InitialSymptom { get; set; }
    public string? SymptomLanguage { get; set; }
    public string? Duration { get; set; }
    public int SeverityScore { get; set; }
    public string? AdditionalSymptoms { get; set; }
    public int SymptomFollowUpCount { get; set; }
    public string? SymptomConversationText { get; set; }
    public string? TempExtractedReportText { get; set; }
    public string? LastAppointmentId { get; set; }
}

public enum BookingStep
{
    Conversational,
    AwaitingSpecialty,
    AwaitingDate,
    AwaitingSlotConfirmation,
    AwaitingSelfBookingConfirmation,
    AwaitingDoctor,
    AwaitingConfirmation,
    AwaitingPatientName,
    AwaitingSymptomFollowUp
}
