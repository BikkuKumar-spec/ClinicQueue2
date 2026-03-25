using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Domain.Entities;
using ClinicQueue.Domain.Enums;
using ClinicQueue.Infrastructure.ExternalServices.AI;
using ClinicQueue.Infrastructure.ExternalServices.Documents;
using ClinicQueue.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClinicQueue.Infrastructure.ExternalServices.WhatsApp;

public class WhatsAppBotProcessor(
    IWhatsAppGateway whatsAppGateway,
    IWhatsAppMediaDownloader whatsAppMediaDownloader,
    IDocumentProcessingGateway documentProcessingGateway,
    IReportSummaryGateway reportSummaryGateway,
    IConversationAiGateway conversationAiGateway,
    IPatientRepository patientRepository,
    IDoctorRepository doctorRepository,
    IAppointmentRepository appointmentRepository,
    IQueueRepository queueRepository,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    ILogger<WhatsAppBotProcessor> logger) : IWhatsAppBotProcessor
{
    // Supported media upload type prefixes sent by the WhatsApp webhook adapter
    private static readonly string[] KnownUploadPrefixes = ["IMAGE_UPLOAD:", "PDF_UPLOAD:"];
    private readonly int _lookAheadDays = GetPositiveInt(configuration["ClinicSettings:BookingWindowDays"], 7);
    private readonly int _reminderMinutesBefore = GetPositiveInt(configuration["ClinicSettings:ReminderMinutesBefore"], 10);

    public async Task ProcessMessageAsync(string from, string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        logger.LogInformation("Message type received: {Type}", ResolveMessageType(input));

        if (string.Equals(input.Trim(), "DOC_UNSUPPORTED", StringComparison.OrdinalIgnoreCase))
        {
            await SendTextAsync(from, "Please upload a PDF or image file only.", cancellationToken);
            return;
        }

        // Reject unsupported upload types (audio, video, stickers, etc.) gracefully
        if (input.Contains("_UPLOAD:", StringComparison.OrdinalIgnoreCase)
            && !KnownUploadPrefixes.Any(p => input.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await SendTextAsync(from, "Sorry, I can only read PDF and image files. Please upload a JPG, PNG, or PDF.", cancellationToken);
            return;
        }

        if (TryExtractMediaUpload(input, out var mediaType, out var mediaId))
        {
            await HandleMediaUploadAsync(from, mediaType, mediaId, cancellationToken);
            return;
        }

        try
        {
            var (englishOutput, displayOutput, _) = await conversationAiGateway.ChatWithDisplayAsync(
                from,
                input.Trim(),
                cancellationToken);

            var replyMessage = BuildReplyMessage(displayOutput.ReplyMessage, englishOutput.ReplyMessage);

            var intent = englishOutput.Intent ?? string.Empty;
            var entities = englishOutput.ExtractedEntities;

            if (string.Equals(intent, "Booking_Confirmed", StringComparison.OrdinalIgnoreCase)
                && entities is not null)
            {
                var bookingCreated = await PerformBookingAsync(entities, from, cancellationToken);
                if (bookingCreated)
                {
                    await conversationAiGateway.ResetSessionAsync(from, cancellationToken);
                }
            }

            await SendTextAsync(from, replyMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Bot error for {Phone}", from);
            await SendTextAsync(from, "Sorry, something went wrong. Please try again.", cancellationToken);
        }
    }

    private async Task<bool> PerformBookingAsync(AiEntities entities, string phoneNumber, CancellationToken cancellationToken)
    {
        try
        {
            var patient = await patientRepository.GetOrCreateByPhoneNumberAsync(
                phoneNumber,
                entities.PatientName ?? "Patient",
                cancellationToken);

            Doctor? doctor = null;
            if (!string.IsNullOrWhiteSpace(entities.PreferredDoctor))
            {
                doctor = await doctorRepository.FindAvailableDoctorByNameAsync(
                    entities.PreferredDoctor,
                    entities.SpecialtyNeeded,
                    cancellationToken);
            }

            if (doctor is null && !string.IsNullOrWhiteSpace(entities.SpecialtyNeeded))
            {
                var doctors = await doctorRepository.GetAvailableBySpecialtyAsync(entities.SpecialtyNeeded, cancellationToken);
                doctor = doctors.FirstOrDefault();
            }

            if (doctor is null)
            {
                logger.LogWarning(
                    "No doctor found for booking. Doctor: {Doctor} Specialty: {Specialty}",
                    entities.PreferredDoctor,
                    entities.SpecialtyNeeded);
                return false;
            }

            var slotTime = ParseSlotTime(entities.PreferredDate, entities.PreferredTime);
            var now = DateTime.Now;
            var maxDate = now.AddDays(_lookAheadDays);
            if (slotTime < now || slotTime > maxDate)
            {
                logger.LogWarning("Slot {Slot} outside valid booking window.", slotTime);
                return false;
            }

            var queuePosition = await appointmentRepository.GetNextQueuePositionAsync(slotTime, doctor.Id, cancellationToken);

            var appointment = Appointment.Create(patient.Id, doctor.Id, slotTime);
            appointment.AssignQueuePosition(queuePosition);
            await appointmentRepository.AddAsync(appointment, cancellationToken);

            var queueEntry = QueueEntry.Create(appointment.Id, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            queueEntry.MoveToPosition(queuePosition);
            await queueRepository.AddAsync(queueEntry, cancellationToken);

            await ScheduleReminderAsync(appointment.Id, slotTime, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Booking created for {Phone} with {Doctor} at {Slot}",
                phoneNumber,
                doctor.Name.Value,
                slotTime);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Booking failed for {Phone}", phoneNumber);
            return false;
        }
    }

    private Task ScheduleReminderAsync(string appointmentId, DateTime slotTime, CancellationToken cancellationToken)
    {
        if (appointmentRepository is not AppointmentRepository concreteAppointmentRepository)
        {
            return Task.CompletedTask;
        }

        var reminderLocal = slotTime.AddMinutes(-_reminderMinutesBefore);
        var reminderUtc = reminderLocal.Kind == DateTimeKind.Utc
            ? reminderLocal
            : reminderLocal.ToUniversalTime();

        var reminder = NotificationReminder.Schedule(appointmentId, NotificationType.Reminder, reminderUtc);
        return concreteAppointmentRepository.AddReminderAsync(reminder, cancellationToken);
    }

    private static DateTime ParseSlotTime(string? preferredDate, string? preferredTime)
    {
        var datePart = string.IsNullOrWhiteSpace(preferredDate)
            ? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : preferredDate.Trim();

        var timePart = string.IsNullOrWhiteSpace(preferredTime)
            ? "09:00"
            : preferredTime.Trim();

        if (DateTime.TryParse(
                $"{datePart} {timePart}",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeLocal,
                out var parsed))
        {
            return parsed;
        }

        return DateTime.Now.AddHours(1);
    }

    private async Task SendTextAsync(string to, string message, CancellationToken cancellationToken)
    {
        var result = await whatsAppGateway.SendTextAsync(to, message, cancellationToken);
        if (!result.IsSuccess)
        {
            logger.LogWarning("Failed to send WhatsApp reply to {To}. Error: {Error}", to, result.Error);
        }
    }

    private async Task HandleMediaUploadAsync(string to, string mediaType, string mediaId, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Downloading media ID: {Id}", mediaId);
            var fileBytes = await whatsAppMediaDownloader.DownloadMediaAsync(mediaId, cancellationToken);
            if (fileBytes.Length == 0)
            {
                await SendTextAsync(to, "I could not download that file. Please send it again.", cancellationToken);
                return;
            }

            var fileName = mediaType == "pdf" ? $"{mediaId}.pdf" : $"{mediaId}.jpg";

            logger.LogInformation("Calling OCR service with {Bytes} bytes", fileBytes.Length);
            var extraction = await documentProcessingGateway.ExtractAsync(fileBytes, fileName, cancellationToken);
            if (!extraction.IsSuccess || extraction.Value is null || !extraction.Value.IsSuccess)
            {
                await SendTextAsync(to, "I could not read text from that file. Please upload a clearer image or PDF.", cancellationToken);
                return;
            }

            var extractedText = extraction.Value.ExtractedText ?? string.Empty;
            logger.LogInformation("OCR result length: {Length}", extractedText.Length);

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                await SendTextAsync(to, "No readable text found in that file. Please try another image or PDF.", cancellationToken);
                return;
            }

            var cleanedText = CleanExtractedText(extractedText);
            if (string.IsNullOrWhiteSpace(cleanedText))
            {
                await SendTextAsync(to, "I could not find useful report text in that file. Please upload a clearer medical report image or PDF.", cancellationToken);
                return;
            }

            // Use the injected IReportSummaryGateway (config-driven, no hardcoded URLs)
            var summaryResult = await reportSummaryGateway.GenerateSummaryAsync(cleanedText, cancellationToken);
            var summaryText = summaryResult.IsSuccess ? summaryResult.Value : null;

            if (string.IsNullOrWhiteSpace(summaryText) || IsLowQualitySummaryReply(summaryText))
            {
                summaryText = BuildFallbackSummary(cleanedText);
            }

            var formattedReply = $"{summaryText}\n\nThis is AI-generated. Please see a doctor.";

            await SendTextAsync(to, formattedReply, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Media processing failed for {Phone} and media {MediaId}", to, mediaId);
            await SendTextAsync(to, "I had trouble processing that file. Please try again.", cancellationToken);
        }
    }

    private static bool TryExtractMediaUpload(string input, out string mediaType, out string mediaId)
    {
        mediaType = string.Empty;
        mediaId = string.Empty;

        if (input.StartsWith("IMAGE_UPLOAD:", StringComparison.OrdinalIgnoreCase))
        {
            mediaType = "image";
            mediaId = input["IMAGE_UPLOAD:".Length..].Trim();
            return !string.IsNullOrWhiteSpace(mediaId);
        }

        if (input.StartsWith("PDF_UPLOAD:", StringComparison.OrdinalIgnoreCase))
        {
            mediaType = "pdf";
            mediaId = input["PDF_UPLOAD:".Length..].Trim();
            return !string.IsNullOrWhiteSpace(mediaId);
        }

        return false;
    }

    private static string ResolveMessageType(string input)
    {
        if (input.StartsWith("IMAGE_UPLOAD:", StringComparison.OrdinalIgnoreCase))
        {
            return "image";
        }

        if (input.StartsWith("PDF_UPLOAD:", StringComparison.OrdinalIgnoreCase))
        {
            return "document";
        }

        return "text";
    }

    private static bool IsLowQualitySummaryReply(string? reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
        {
            return true;
        }

        var value = reply.Trim();
        var lowered = value.ToLowerInvariant();
        var hasBulletLikeContent = value.Contains("\n-")
            || value.Contains("\n•")
            || value.Contains("\n*")
            || value.Contains("; ");

        return lowered.Contains("i apologize")
            || lowered.Contains("could you please rephrase")
            || lowered.Contains("having trouble processing")
            || lowered.Contains("please rephrase")
            || lowered.Contains("i didn't quite understand")
            || lowered.Contains("service temporarily unavailable")
            || lowered.Contains("your medical summary")
            || lowered.Contains("summary of your lab report")
            || lowered.Contains("here are the key points")
            || lowered.Contains("key points:")
            || (value.EndsWith(':') && !hasBulletLikeContent)
            || (lowered.Contains("summary") && !hasBulletLikeContent)
            || lowered.Length < 40;
    }

    private static string BuildFallbackSummary(string extractedText)
    {
        var lines = extractedText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length >= 8)
            .Take(5)
            .ToList();

        if (lines.Count == 0)
        {
            return "I could read the document, but the report text is unclear. Please upload a clearer image or PDF.";
        }

        return "Here is a quick summary from your uploaded report:\n- " + string.Join("\n- ", lines);
    }

    private static string AddAiVerificationNote(string summary)
    {
        const string note = "\n\nNote: This is an AI-generated summary. Please verify with your doctor.";

        if (string.IsNullOrWhiteSpace(summary))
        {
            return "I could not generate a clear summary." + note;
        }

        if (summary.Contains("AI-generated summary", StringComparison.OrdinalIgnoreCase)
            || summary.Contains("verify with your doctor", StringComparison.OrdinalIgnoreCase))
        {
            return summary;
        }

        return summary.TrimEnd() + note;
    }

    private static bool IsLikelyMedicalReport(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lowered = text.ToLowerInvariant();
        var medicalKeywords = new[]
        {
            "patient", "doctor", "hospital", "clinic", "medical", "diagnosis", "prescription",
            "medicine", "tablet", "capsule", "dosage", "mg", "ml", "report", "lab", "test",
            "blood", "urine", "glucose", "hemoglobin", "wbc", "rbc", "xray", "mri", "ct", "ultrasound"
        };

        var hits = medicalKeywords.Count(lowered.Contains);
        return hits >= 2;
    }

    // SummarizeDocumentAsync removed — replaced by IReportSummaryGateway.GenerateSummaryAsync



    private static string CleanExtractedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var noisyFragments = new[]
        {
            "c:\\users", "ollama list", "name id size", "modified", "read only", "read-only",
            "command", "powershell", "terminal", "prompt"
        };

        var cleanedLines = text
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length >= 3)
            .Where(x => !noisyFragments.Any(fragment => x.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .Where(x => x.Count(char.IsLetter) >= 3)
            .Take(40)
            .ToList();

        return string.Join("\n", cleanedLines);
    }

    private static string BuildReplyMessage(string? displayReply, string? englishReply)
    {
        if (!string.IsNullOrWhiteSpace(displayReply))
        {
            return displayReply.Trim();
        }

        if (!string.IsNullOrWhiteSpace(englishReply))
        {
            return englishReply.Trim();
        }

        return "How can I help you?";
    }

    private static int GetPositiveInt(string? rawValue, int fallback)
    {
        return int.TryParse(rawValue, out var parsed) && parsed > 0
            ? parsed
            : fallback;
    }
}

