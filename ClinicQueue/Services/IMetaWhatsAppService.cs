namespace ClinicQueue.Services
{
    public interface IMetaWhatsAppService
    {
        // Basic messaging
        Task<string> SendTextMessageAsync(string toPhone, string message);
        
        // Interactive messages
        Task<string> SendButtonMessageAsync(string toPhone, string bodyText, List<ButtonDto> buttons, string? headerText = null);
        Task<string> SendListMessageAsync(string toPhone, string bodyText, string buttonText, List<ListSectionDto> sections, string? headerText = null);
        
        // Template messages
        Task<string> SendTemplateMessageAsync(string toPhone, string templateName, string languageCode = "en");
        
        // Message formatting (keep same as Twilio for compatibility)
        string GetBookingConfirmation(string patientName, DateTime slotTime);
        string GetQueueUpdate(string doctorName, int position, int waitMinutes);
        string GetDoctorReady(string patientName);
        string GetAppointmentMovedEarlier(string patientName, DateTime newSlotTime);
        string GetArrivalReminder(string patientName, DateTime slotTime);
        string GetArrivalConfirmation(string patientName, string doctorName, int queuePosition, int waitMinutes);
        string GetNextInLineNotification(string doctorName);
        string GetNowServingNotification(string patientName);
        
        // Notification methods
        Task SendYouAreNextNotificationAsync(string phone, string patientName, int position, int estimatedWaitMinutes);
        Task SendArrivalReminderAsync(string phone, string patientName, DateTime slotTime);
    }

    // DTO classes for interactive messages
    public class ButtonDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public class ListSectionDto
    {
        public string Title { get; set; } = string.Empty;
        public List<ListRowDto> Rows { get; set; } = new();
    }

    public class ListRowDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
