using ClinicQueue.Data;
using Dapper;
using ClinicQueue.Shared.DTOs;
using ClinicQueue.Shared.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ClinicQueue.Shared.Utilities;

namespace ClinicQueue.Services
{
    public class WhatsAppBotService
    {
        private readonly IAppointmentService _appointmentService;
        private readonly IPatientService _patientService;
        private readonly IQueueService _queueService;
        private readonly IMetaWhatsAppService _metaService;
        private readonly IReminderService _reminderService;
        private readonly IAISymptomService _aiService;
        private readonly DatabaseService _db;
        private readonly ITranslationService _translator;
        private readonly BotSessionStore _sessions;

        public WhatsAppBotService(
            IAppointmentService appointmentService,
            IPatientService patientService,
            IQueueService queueService,
            IMetaWhatsAppService metaService,
            IReminderService reminderService,
            IAISymptomService aiService,
            DatabaseService db,
            ITranslationService translator,
            BotSessionStore sessions)
        {
            _appointmentService = appointmentService;
            _patientService = patientService;
            _queueService = queueService;
            _metaService = metaService;
            _reminderService = reminderService;
            _aiService = aiService;
            _db = db;
            _translator = translator;
            _sessions = sessions;
        }

        public class BookingSession
        {
            public string? PatientName { get; set; }         // In user's language (shown to user)
            public string? PatientNameEnglish { get; set; }  // Always English (saved to DB / dashboard)
            public string? Specialty { get; set; }
            public string? DoctorName { get; set; }
            public string? RescheduleAppointmentId { get; set; }
            public DateTime? SelectedDate { get; set; }
            public SlotDto? SelectedSlot { get; set; }
            public List<SlotDto>? AvailableSlots { get; set; }
            public SymptomAnalysis? SymptomAnalysis { get; set; }
            public string? InitialSymptom { get; set; }
            public string? SymptomLanguage { get; set; }
            public string? Duration { get; set; }
            public int SeverityScore { get; set; }
            public string? AdditionalSymptoms { get; set; }
        }

        // ─── Translation Helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Translates English bot text → user's language.
        /// If lang is English or empty, returns the original text immediately (no API call).
        /// Never throws — falls back to original English text on any error.
        /// </summary>
        private async Task<string> T(string englishText, string lang)
        {
            if (string.IsNullOrEmpty(lang) || lang == "eng_Latn")
                return englishText;
            try
            {
                // ✅ Strip leading emojis before translation and re-add after
                // Translation microservice often drops emojis from the start of text
                var leadingEmojis = ExtractLeadingEmojis(englishText);
                var textToTranslate = englishText.Substring(leadingEmojis.Length);

                if (string.IsNullOrWhiteSpace(textToTranslate))
                    return englishText;

                var result = await _translator.FromEnglishAsync(textToTranslate, lang);
                Console.WriteLine($"[T()] lang='{lang}' | in='{englishText}' | out='{leadingEmojis}{result}'");
                return string.IsNullOrWhiteSpace(result) ? englishText : leadingEmojis + result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TRANSLATE ERROR] lang={lang} error={ex.Message}");
                return englishText;
            }
        }

        /// <summary>
        /// Extracts leading emoji characters from a string so they can be preserved after translation.
        /// Emojis are in Unicode ranges U+1F000 and above, or U+2600-U+27BF (misc symbols).
        /// </summary>
        private static string ExtractLeadingEmojis(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new System.Text.StringBuilder();
            int i = 0;
            while (i < text.Length)
            {
                // Check for surrogate pairs (most emojis)
                if (i + 1 < text.Length && char.IsHighSurrogate(text[i]) && char.IsLowSurrogate(text[i + 1]))
                {
                    int codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
                    if (codePoint >= 0x1F000)
                    {
                        sb.Append(text[i]);
                        sb.Append(text[i + 1]);
                        i += 2;
                        // Also consume trailing space after emoji
                        if (i < text.Length && text[i] == ' ') { sb.Append(' '); i++; }
                        continue;
                    }
                    break;
                }
                // Check for single-char emoji symbols (U+2600–U+27BF)
                if (text[i] >= '\u2600' && text[i] <= '\u27BF')
                {
                    sb.Append(text[i]);
                    i++;
                    if (i < text.Length && text[i] == ' ') { sb.Append(' '); i++; }
                    continue;
                }
                // Zero-width joiner — skip and continue
                if (text[i] == '\u200D') { i++; continue; }
                break;
            }
            return sb.ToString();
        }



        // ─── Language Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Gets the current language for a phone number.
        /// Prefers the language stored in the active session data,
        /// then falls back to the store-level detected language.
        /// </summary>
        private string GetLang(string phoneNumber, BookingSession? data = null)
        {
            // 1. Check the session data first (it holds the primary 'choice' for the current flow)
            if (!string.IsNullOrEmpty(data?.SymptomLanguage))
                return data.SymptomLanguage;

            // 2. Fall back to the persistent store
            var storeLang = _sessions.GetLanguage(phoneNumber);
            return !string.IsNullOrEmpty(storeLang) ? storeLang : "eng_Latn";
        }

        /// <summary>
        /// Supported languages. Any other detected language falls back to English.
        /// </summary>
        private static readonly HashSet<string> SupportedLanguages = new(StringComparer.OrdinalIgnoreCase)
        {
            "eng_Latn",  // English
            "hin_Deva",  // Hindi
        };

        /// <summary>
        /// Detects language from real user text, persists it in the store,
        /// and also updates the active session's SymptomLanguage if one exists.
        /// Only Hindi and English are supported — all other languages fall back to English.
        /// Returns the detected language code (or "eng_Latn" on failure/unsupported).
        /// </summary>
        private async Task<string> DetectAndStore(string phoneNumber, string text)
        {
            try
            {
                var detected = await _translator.DetectLanguageAsync(text);
                if (!string.IsNullOrWhiteSpace(detected))
                {
                    // ✅ Only allow Hindi and English — everything else falls back to English
                    if (!SupportedLanguages.Contains(detected))
                    {
                        detected = "eng_Latn";
                    }

                    // 🔥 STICKY LOGIC: If user types Hindi, lock it in. 
                    // If they type Roman text, only overwrite if we DON'T have a locked Hindi session.
                    var current = _sessions.GetLanguage(phoneNumber);
                    if (detected == "hin_Deva" || current != "hin_Deva")
                    {
                        _sessions.SetLanguage(phoneNumber, detected);
                        Console.WriteLine($"[LANG DETECTED] {phoneNumber} → '{detected}'");
                    }

                    // Always sync the detected language into the active session so it's
                    // immediately available in ProcessSessionInput on this same request
                    if (_sessions.TryGet(phoneNumber, out var existingSession))
                    {
                        // Only switch away from Hindi if the NEW detection is Hindi or we are resetting
                        if (detected == "hin_Deva" || existingSession.Data.SymptomLanguage != "hin_Deva")
                        {
                            existingSession.Data.SymptomLanguage = detected;
                            _sessions.Set(phoneNumber, existingSession.State, existingSession.Data);
                        }
                    }

                    return detected;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LANG DETECT ERROR] {ex.Message}");
            }

            return _sessions.GetLanguage(phoneNumber);
        }

        // ─── Welcome ──────────────────────────────────────────────────────────────

        private async Task<string> SendWelcomeMessage(string phoneNumber, string? lang = null)
        {
            lang ??= _sessions.GetLanguage(phoneNumber);
            var text = await T("Welcome to Dr. Sharma's Clinic! I am your AI assistant. How can I help you today? Please describe any symptoms you are experiencing.", lang);
            await _metaService.SendTextMessageAsync(phoneNumber, text);
            
            _sessions.Set(phoneNumber, "CONVERSATIONAL", new BookingSession { SymptomLanguage = lang });
            return "CONVERSATIONAL";
        }

        // ─── Main Entry Point ─────────────────────────────────────────────────────

        public async Task<string> HandleMessageAsync(string phoneNumber, string input)
        {
            Console.WriteLine($"[BOT] Incoming from {phoneNumber}: '{input}'");

            var normalizedInput = input.Trim().ToUpper();

            // Is this real user text (not a button press, system ID, or list selection)?
            // Doctor names like "Dr. Sharma", dates like "2026-02-20", slot times like "2026-02-20 10:30"
            // are list reply IDs — detecting language from these would incorrectly overwrite Hindi with English
            var isRealText = !normalizedInput.StartsWith("BTN_") &&
                             !normalizedInput.StartsWith("REMINDER_") &&
                             !normalizedInput.StartsWith("DR.") &&
                             !System.Text.RegularExpressions.Regex.IsMatch(input.Trim(), @"^\d{4}-\d{2}-\d{2}") &&
                             input.Trim().Length >= 1;

            // ── Step 1: Detect language from real user text FIRST ──────────────────
            // ✅ IMPROVED: Always detect language from real user text, even if a session 
            // is active. This allows the bot to "follow" the user if they switch languages
            // mid-conversation (e.g. starting in English and switching to Hindi/Hinglish).
            string lang;
            if (isRealText)
            {
                lang = await DetectAndStore(phoneNumber, input);
                Console.WriteLine($"[BOT] Language for {phoneNumber} = '{lang}' (Detected)");
            }
            else
            {
                lang = _sessions.GetLanguage(phoneNumber);
            }

            // ── PRIORITY 1: Reminder buttons ───────────────────────────────────────
            if (normalizedInput.StartsWith("REMINDER_YES_"))
            {
                await _reminderService.HandleReminderYesAsync(input.Substring("reminder_yes_".Length), phoneNumber);
                return "REMINDER_CONFIRMED";
            }
            if (normalizedInput.StartsWith("REMINDER_NO_"))
            {
                await _reminderService.HandleReminderNoAsync(input.Substring("reminder_no_".Length), phoneNumber);
                return "REMINDER_CANCELLED";
            }

            if (_sessions.TryGet(phoneNumber, out var session))
            {
                // ✅ Allow universal escape words
                var isGreeting = normalizedInput is "EXIT" or "CANCEL" or "RESTART" or "MENU"
                                 or "HI" or "HELLO" or "NAMASTE" or "HELO" or "BYE" or "STOP";
                var isDevanagariGreeting = input.Trim() is "नमस्ते" or "नमस्कार" or "हेलो" or "हाय";

                if (isGreeting || isDevanagariGreeting)
                {
                    _sessions.Remove(phoneNumber);
                    await _aiService.ResetSessionAsync(phoneNumber);
                    return await SendWelcomeMessage(phoneNumber, lang);
                }
                return await ProcessSessionInput(phoneNumber, input, session.State, session.Data, lang);
            }

            // ── Default: Start new conversational session ─────────────────────────
            await _aiService.ResetSessionAsync(phoneNumber);
            return await SendWelcomeMessage(phoneNumber, lang);
        }

        // ─── Session Processing ───────────────────────────────────────────────────

        private async Task<string> ProcessSessionInput(string phoneNumber, string input, string state, BookingSession? data, string currentLanguage)
        {
            data ??= new BookingSession();

            // Stick to Hindi if detected once
            if (currentLanguage == "hin_Deva" || string.IsNullOrEmpty(data.SymptomLanguage))
            {
                data.SymptomLanguage = currentLanguage;
            }
            var lang = data.SymptomLanguage;
            _sessions.SetLanguage(phoneNumber, lang);

            // 1. Inject Context (Internal data for the AI to know about our clinic state)
            var context = await GetClinicContext(data);
            var augmentedInput = $"[CONTEXT: {context}] User Message: {input}";

            try
            {
                // 2. Call AI Service
                var (english, display, aiLang) = await _aiService.ChatWithDisplayAsync(phoneNumber, augmentedInput);

                // Sync language if AI detected Hindi
                if (aiLang == "hin_Deva")
                {
                    lang = "hin_Deva";
                    data.SymptomLanguage = lang;
                    _sessions.SetLanguage(phoneNumber, lang);
                }

                // 3. Extract and Sync Entities
                await SyncEntities(data, english.ExtractedEntities, lang);

                // 4. Handle Intent
                switch (english.Intent)
                {
                    case "Booking_Confirmed":
                        // Final safety check before actual booking
                        if (data.SelectedDate != null && !string.IsNullOrEmpty(data.DoctorName) && !string.IsNullOrEmpty(data.PatientName) && data.SelectedSlot != null)
                        {
                            await _metaService.SendTextMessageAsync(phoneNumber, display.ReplyMessage);
                            return await CompleteBooking(phoneNumber, data);
                        }
                        // Fallthrough to standard reply if missing info
                        goto default;

                    default:
                        await _metaService.SendTextMessageAsync(phoneNumber, display.ReplyMessage);
                        _sessions.Set(phoneNumber, "CONVERSATIONAL", data);
                        return "CONVERSATIONAL";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOT ERROR] {ex.Message}");
                var errorMsg = await T("I'm sorry, I'm having trouble connecting to the clinic system. Please try again in a moment.", lang);
                await _metaService.SendTextMessageAsync(phoneNumber, errorMsg);
                return "ERROR";
            }
        }

        private async Task<string> GetClinicContext(BookingSession data)
        {
            var ctx = $"Current Date: {DateTime.Now:yyyy-MM-dd}. ";

            // If we have a specialty, provide doctors
            if (!string.IsNullOrEmpty(data.Specialty))
            {
                using var conn = _db.GetConnection();
                var doctors = (await conn.QueryAsync<string>(
                    "SELECT d.name FROM doctors d JOIN specialties s ON d.specialty_id = s.id WHERE s.name LIKE @s AND d.is_active = 1",
                    new { s = $"%{data.Specialty}%" })).ToList();
                
                if (doctors.Any())
                    ctx += $"Available Doctors in {data.Specialty}: {string.Join(", ", doctors)}. ";
            }

            // If we have doctor & date, provide slots
            if (!string.IsNullOrEmpty(data.DoctorName) && data.SelectedDate.HasValue)
            {
                // Basic cleanup of doctor name to match DB (e.g. "Dr. Ajay" -> "Ajay")
                var searchName = data.DoctorName.Replace("Dr.", "").Trim();
                var slots = await _appointmentService.GetAvailableSlotsAsync(data.SelectedDate.Value, searchName);
                var available = slots.Where(s => s.IsAvailable).Select(s => s.Time.ToString("HH:mm")).ToList();
                if (available.Any())
                    ctx += $"Available Slots for {data.DoctorName} on {data.SelectedDate.Value:yyyy-MM-dd}: {string.Join(", ", available)}. ";
                else
                    ctx += $"No slots available for {data.DoctorName} on {data.SelectedDate.Value:yyyy-MM-dd}. ";
            }

            return ctx;
        }

        private async Task SyncEntities(BookingSession data, AiEntities entities, string lang)
        {
            if (!string.IsNullOrEmpty(entities.SpecialtyNeeded)) data.Specialty = entities.SpecialtyNeeded;
            if (!string.IsNullOrEmpty(entities.PreferredDoctor)) data.DoctorName = entities.PreferredDoctor;
            
            if (!string.IsNullOrEmpty(entities.PatientName))
            {
                data.PatientName = entities.PatientName.ToProperCase();
                // If user is in Hindi, the name might be in Devanagari. Translate for DB.
                if (lang == "hin_Deva")
                {
                    try
                    {
                        var (englishName, _) = await _translator.ToEnglishAsync(entities.PatientName);
                        data.PatientNameEnglish = englishName.ToProperCase();
                    }
                    catch { data.PatientNameEnglish = data.PatientName; }
                }
                else
                {
                    data.PatientNameEnglish = data.PatientName;
                }
            }

            if (!string.IsNullOrEmpty(entities.PreferredDate))
            {
                if (DateTime.TryParse(entities.PreferredDate, out var d))
                {
                   data.SelectedDate = d;
                }
            }

            if (!string.IsNullOrEmpty(entities.PreferredTime) && data.SelectedDate.HasValue && !string.IsNullOrEmpty(data.DoctorName))
            {
                var timeStr = $"{data.SelectedDate.Value:yyyy-MM-dd} {entities.PreferredTime}";
                if (DateTime.TryParse(timeStr, out var selectedTime))
                {
                    data.SelectedSlot = new SlotDto { Time = selectedTime, IsAvailable = true };
                }
            }
        }

        private async Task<string> CompleteBooking(string phoneNumber, BookingSession data)
        {
            var lang = GetLang(phoneNumber, data);
            try
            {
                var patient = await _patientService.GetByPhoneAsync(phoneNumber);

                if (!string.IsNullOrEmpty(data.RescheduleAppointmentId))
                {
                    await _appointmentService.RescheduleAsync(data.RescheduleAppointmentId, data.SelectedSlot!.Time);
                    await _metaService.SendTextMessageAsync(phoneNumber,
                        await T($"🔄 *Appointment Rescheduled*\n\n👨‍⚕️ {data.DoctorName}\n📅 {data.SelectedSlot.Time:dddd, dd MMM}\n⏰ {data.SelectedSlot.Time:hh:mm tt}\n\nSee you then!", lang));
                    _sessions.Remove(phoneNumber);
                    return "RESCHEDULE_COMPLETE";
                }

                await _appointmentService.CreateAsync(new CreateAppointmentRequest
                {
                    PatientId   = patient?.Id ?? string.Empty,
                    PhoneNumber = phoneNumber,
                    PatientName = data.PatientNameEnglish ?? data.PatientName ?? "Unknown",
                    SlotTime    = data.SelectedSlot!.Time,
                    DoctorName  = data.DoctorName ?? "General",
                    Specialty   = data.Specialty ?? "General"
                });

                _sessions.Remove(phoneNumber);
                await _metaService.SendTextMessageAsync(phoneNumber,
                    await T($"🎉 *Appointment Confirmed!*\n\n📅 {data.SelectedDate:dddd, dd MMM}\n⏰ {data.SelectedSlot.Time:hh:mm tt}\n👨‍⚕️ {data.DoctorName}\n\nPlease arrive 10 mins early.\nWe will notify you 20 mins before your slot.\n\n_ClinicQueue_", lang));
                return "BOOKING_COMPLETE";
            }
            catch (Exception ex)
            {
                _sessions.Remove(phoneNumber);
                await _metaService.SendTextMessageAsync(phoneNumber,
                    await T($"❌ *Booking Failed*\n\nError: {ex.Message}\n\nPlease try again.", lang));
                return "ERROR";
            }
        }
    }
}