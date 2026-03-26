using ClinicQueue.Application.Interfaces;
using ClinicQueue.Infrastructure.ExternalServices.AI;
using ClinicQueue.Infrastructure.ExternalServices.WhatsApp;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace ClinicQueue.API.Controllers;

[ApiController]
[Route("api/voice")]
public class VoiceController(
    IConversationAiGateway conversationAiGateway,
    IPatientRepository patientRepository,
    IDoctorRepository doctorRepository,
    IAppointmentRepository appointmentRepository,
    IQueueRepository queueRepository,
    IUnitOfWork unitOfWork,
    IConfiguration configuration,
    ILogger<VoiceController> logger) : ControllerBase
{
    [HttpPost("chat")]
    public async Task<ActionResult<VoiceChatResponse>> Chat(
        [FromBody] VoiceChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Transcript) || 
            string.IsNullOrWhiteSpace(request.SessionId))
        {
            return BadRequest("Transcript and SessionId are required.");
        }

        try
        {
            var (englishOutput, _, _) = await conversationAiGateway.ChatWithDisplayAsync(
                request.SessionId,
                request.Transcript.Trim(),
                cancellationToken);

            var intent = englishOutput.Intent ?? "Other";
            var bookingCompleted = false;

            if (string.Equals(intent, "Booking_Confirmed", StringComparison.OrdinalIgnoreCase)
                && englishOutput.ExtractedEntities is not null)
            {
                // Reuse exact same booking logic as WhatsAppBotProcessor
                bookingCompleted = await PerformBookingAsync(
                    englishOutput.ExtractedEntities,
                    request.SessionId,  // used as phone/patient identifier
                    cancellationToken);

                if (bookingCompleted)
                {
                    await conversationAiGateway.ResetSessionAsync(
                        request.SessionId, 
                        cancellationToken);
                }
            }

            return Ok(new VoiceChatResponse
            {
                ResponseText = StripMarkdown(englishOutput.ReplyMessage),
                SessionId = request.SessionId,
                Intent = intent,
                BookingCompleted = bookingCompleted,
                MissingInfo = englishOutput.MissingInfo
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voice chat failed for session {SessionId}", request.SessionId);
            return StatusCode(500, new VoiceChatResponse
            {
                ResponseText = "Something went wrong. Please try again.",
                SessionId = request.SessionId,
                Intent = "Error",
                BookingCompleted = false
            });
        }
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(
        [FromQuery] string sessionId,
        CancellationToken cancellationToken)
    {
        await conversationAiGateway.ResetSessionAsync(sessionId, cancellationToken);
        return Ok();
    }

    // Copied from WhatsAppBotProcessor — consider extracting to a shared service later
    private async Task<bool> PerformBookingAsync(
        AiEntities entities, 
        string sessionId, 
        CancellationToken cancellationToken)
    {
        try
        {
            var patient = await patientRepository.GetOrCreateByPhoneNumberAsync(
                sessionId,  // voice sessions use sessionId as identifier
                entities.PatientName ?? "Voice Patient",
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
                var doctors = await doctorRepository.GetAvailableBySpecialtyAsync(
                    entities.SpecialtyNeeded, 
                    cancellationToken);
                doctor = doctors.FirstOrDefault();
            }

            if (doctor is null)
            {
                logger.LogWarning(
                    "No doctor found. Doctor: {Doctor}, Specialty: {Specialty}",
                    entities.PreferredDoctor,
                    entities.SpecialtyNeeded);
                return false;
            }

            var slotTime = ParseSlotTime(entities.PreferredDate, entities.PreferredTime);
            var now = DateTime.Now;

            if (slotTime < now || slotTime > now.AddDays(7))
            {
                logger.LogWarning("Slot {Slot} outside booking window.", slotTime);
                return false;
            }

            var queuePosition = await appointmentRepository
                .GetNextQueuePositionAsync(slotTime, doctor.Id, cancellationToken);

            var appointment = Appointment.Create(patient.Id, doctor.Id, slotTime);
            appointment.AssignQueuePosition(queuePosition);
            await appointmentRepository.AddAsync(appointment, cancellationToken);

            var queueEntry = QueueEntry.Create(
                appointment.Id, 
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            queueEntry.MoveToPosition(queuePosition);
            await queueRepository.AddAsync(queueEntry, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Voice booking created for session {Session} with {Doctor} at {Slot}",
                sessionId, doctor.Name.Value, slotTime);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Voice booking failed for session {Session}", sessionId);
            return false;
        }
    }

    private static string StripMarkdown(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Remove bold/italic markers
        text = Regex.Replace(text, @"[\*_]{1,3}(.+?)[\*_]{1,3}", "$1");
        // Convert bullet points to natural pauses
        text = Regex.Replace(text, @"^\s*[-•*]\s+", "", RegexOptions.Multiline);
        // Remove headers
        text = Regex.Replace(text, @"^#{1,6}\s+", "", RegexOptions.Multiline);
        // Collapse multiple newlines to a single space
        text = Regex.Replace(text, @"\n+", " ");

        return text.Trim();
    }

    private static DateTime ParseSlotTime(string? preferredDate, string? preferredTime)
    {
        var datePart = string.IsNullOrWhiteSpace(preferredDate)
            ? DateTime.Now.ToString("yyyy-MM-dd")
            : preferredDate.Trim();

        var timePart = string.IsNullOrWhiteSpace(preferredTime)
            ? "09:00"
            : preferredTime.Trim();

        return DateTime.TryParse(
            $"{datePart} {timePart}",
            out var parsed) ? parsed : DateTime.Now.AddHours(1);
    }
}

// Request/Response models
public sealed class VoiceChatRequest
{
    public string Transcript { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
}

public sealed class VoiceChatResponse
{
    public string ResponseText { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public bool BookingCompleted { get; set; }
    public List<string> MissingInfo { get; set; } = new();
}

//adding a random comment to fix empty github file issue