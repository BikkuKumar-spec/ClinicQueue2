using System.Collections.Concurrent;

namespace ClinicQueue.Services
{
    public class BotSessionStore
    {
        private readonly ConcurrentDictionary<string, (string State, WhatsAppBotService.BookingSession Data)>
            _sessions = new();

        private readonly ConcurrentDictionary<string, string>
            _userLanguages = new();

        // ✅ Per-user semaphore — ensures only ONE message per phone number is
        // processes at a time. Without this, a slow Llama response races against
        // the user's next message and overwrites the advanced session state.
        private readonly ConcurrentDictionary<string, SemaphoreSlim>
            _locks = new();

        /// <summary>
        /// Returns the per-user semaphore (creates one if it doesn't exist).
        /// Caller must always release it in a finally block.
        /// </summary>
        public SemaphoreSlim GetLock(string phoneNumber)
            => _locks.GetOrAdd(phoneNumber, _ => new SemaphoreSlim(1, 1));

        public bool TryGet(string phoneNumber, out (string State, WhatsAppBotService.BookingSession Data) session)
            => _sessions.TryGetValue(phoneNumber, out session);

        public void Set(string phoneNumber, string state, WhatsAppBotService.BookingSession data)
            => _sessions[phoneNumber] = (state, data);

        public bool Remove(string phoneNumber)
            => _sessions.TryRemove(phoneNumber, out _);

        /// <summary>
        /// Returns the user's last detected language, defaulting to English.
        /// </summary>
        public string GetLanguage(string phoneNumber)
            => _userLanguages.TryGetValue(phoneNumber, out var lang) ? lang : "eng_Latn";

        /// <summary>
        /// Called every time a real text message arrives to update language.
        /// </summary>
        public void SetLanguage(string phoneNumber, string language)
            => _userLanguages[phoneNumber] = language;

        /// <summary>
        /// Returns true if the stored language for this user is English (or not yet set).
        /// </summary>
        public bool IsEnglish(string phoneNumber)
        {
            var lang = GetLanguage(phoneNumber);
            return string.IsNullOrEmpty(lang) || lang == "eng_Latn";
        }
    }
}