using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ClinicQueue.Data;
using Dapper;

namespace ClinicQueue.Services
{
    public class MetaWhatsAppService : IMetaWhatsAppService
    {
        private readonly string _phoneNumberId;
        private readonly string _accessToken;
        private readonly string _apiVersion;
        private readonly HttpClient _httpClient;
        private readonly DatabaseService _db;
        private readonly ILogger<MetaWhatsAppService> _logger;

        public MetaWhatsAppService(
            string phoneNumberId,
            string accessToken,
            string apiVersion,
            DatabaseService db,
            HttpClient httpClient,
            ILogger<MetaWhatsAppService> logger)
        {
            _phoneNumberId = phoneNumberId;
            _accessToken = accessToken;
            _apiVersion = apiVersion;
            _db = db;
            _httpClient = httpClient;
            _logger = logger;

            _logger.LogInformation($"PHONE ID: [{_phoneNumberId}]");
_logger.LogInformation($"TOKEN LENGTH: {_accessToken?.Length}");
_logger.LogInformation($"TOKEN START: {_accessToken?.Substring(0,5)}");

            // Configure HttpClient
            _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
        }

        private string GetApiUrl() => $"https://graph.facebook.com/{_apiVersion}/{_phoneNumberId}/messages";

        public async Task<string> SendTextMessageAsync(string toPhone, string message)
        {
            try
            {
                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = toPhone.Replace("+", "").Replace(" ", ""),
                    type = "text",
                    text = new { body = message }
                };

                var response = await SendRequestAsync(payload);
                
                // Log to database
                await LogMessageAsync(toPhone, message, "sent");

                return response.messages?[0]?.id ?? "unknown";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending WhatsApp text message: {ex.Message}");
                await LogMessageAsync(toPhone, message, "failed");
                throw;
            }
        }

        public async Task<string> SendButtonMessageAsync(string toPhone, string bodyText, List<ButtonDto> buttons, string? headerText = null)
        {
            try
            {
                if (buttons.Count > 3)
                    throw new ArgumentException("Meta WhatsApp supports maximum 3 buttons");

                var interactiveButtons = buttons.Select(b => new
                {
                    type = "reply",
                    reply = new
                    {
                        id = b.Id,
                        title = b.Title.Length > 20 ? b.Title.Substring(0, 20) : b.Title
                    }
                }).ToList();

                object interactive;
                
                if (!string.IsNullOrEmpty(headerText))
                {
                    interactive = new
                    {
                        type = "button",
                        header = new { type = "text", text = headerText },
                        body = new { text = bodyText },
                        action = new { buttons = interactiveButtons }
                    };
                }
                else
                {
                    interactive = new
                    {
                        type = "button",
                        body = new { text = bodyText },
                        action = new { buttons = interactiveButtons }
                    };
                }

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = toPhone.Replace("+", "").Replace(" ", ""),
                    type = "interactive",
                    interactive
                };

                var response = await SendRequestAsync(payload);
                await LogMessageAsync(toPhone, $"[BUTTON] {bodyText}", "sent");

                return response.messages?[0]?.id ?? "unknown";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending button message: {ex.Message}");
                await LogMessageAsync(toPhone, $"[BUTTON] {bodyText}", "failed");
                throw;
            }
        }

        public async Task<string> SendListMessageAsync(string toPhone, string bodyText, string buttonText, List<ListSectionDto> sections, string? headerText = null)
        {
            try
            {
                // Meta Limit: Total row count across all sections must not exceed 10
                var totalRows = sections.Sum(s => s.Rows.Count);
                if (totalRows > 10)
                {
                    _logger.LogWarning($"List message to {toPhone} has {totalRows} rows. Truncating to 10.");
                }

                var metaSections = new List<object>();
                int rowsAdded = 0;
                
                foreach (var s in sections)
                {
                    if (rowsAdded >= 10) break;
                    
                    var sectionRows = s.Rows.Take(10 - rowsAdded).Select(r => new
                    {
                        id = r.Id,
                        title = r.Title.Length > 24 ? r.Title.Substring(0, 24) : r.Title,
                        description = r.Description?.Length > 72 ? r.Description.Substring(0, 72) : r.Description
                    }).ToList();

                    metaSections.Add(new
                    {
                        title = s.Title,
                        rows = sectionRows
                    });
                    
                    rowsAdded += sectionRows.Count;
                }

                object interactive;
                
                if (!string.IsNullOrEmpty(headerText))
                {
                    interactive = new
                    {
                        type = "list",
                        header = new { type = "text", text = headerText },
                        body = new { text = bodyText },
                        action = new
                        {
                            button = buttonText.Length > 20 ? buttonText.Substring(0, 20) : buttonText,
                            sections = metaSections
                        }
                    };
                }
                else
                {
                    interactive = new
                    {
                        type = "list",
                        body = new { text = bodyText },
                        action = new
                        {
                            button = buttonText.Length > 20 ? buttonText.Substring(0, 20) : buttonText,
                            sections = metaSections
                        }
                    };
                }

                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = toPhone.Replace("+", "").Replace(" ", ""),
                    type = "interactive",
                    interactive
                };

                var response = await SendRequestAsync(payload);
                await LogMessageAsync(toPhone, $"[LIST] {bodyText}", "sent");

                return response.messages?[0]?.id ?? "unknown";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending list message: {ex.Message}");
                await LogMessageAsync(toPhone, $"[LIST] {bodyText}", "failed");
                throw;
            }
        }

        public async Task<string> SendTemplateMessageAsync(string toPhone, string templateName, string languageCode = "en")
        {
            try
            {
                var payload = new
                {
                    messaging_product = "whatsapp",
                    recipient_type = "individual",
                    to = toPhone.Replace("+", "").Replace(" ", ""),
                    type = "template",
                    template = new
                    {
                        name = templateName,
                        language = new { code = languageCode }
                    }
                };

                var response = await SendRequestAsync(payload);
                await LogMessageAsync(toPhone, $"[TEMPLATE] {templateName}", "sent");

                return response.messages?[0]?.id ?? "unknown";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending template message: {ex.Message}");
                await LogMessageAsync(toPhone, $"[TEMPLATE] {templateName}", "failed");
                throw;
            }
        }

        private async Task<MetaApiResponse> SendRequestAsync(object payload)
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

            _logger.LogInformation($"Sending to Meta API: {json}");

            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(GetApiUrl(), content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogInformation($"Meta API Response: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Meta API error: {response.StatusCode} - {responseBody}");
            }

            return JsonSerializer.Deserialize<MetaApiResponse>(responseBody) 
                ?? throw new Exception("Failed to parse Meta API response");
        }

        private async Task LogMessageAsync(string phone, string message, string status)
        {
            try
            {
                using var connection = _db.GetConnection();
                await connection.ExecuteAsync(@"
                    INSERT INTO notification_log (id, phone, message, status, sent_at)
                    VALUES (@Id, @Phone, @Message, @Status, @SentAt)",
                    new
                    {
                        Id = Guid.NewGuid().ToString(),
                        Phone = phone,
                        Message = message,
                        Status = status,
                        SentAt = DateTime.UtcNow
                    }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to log message: {ex.Message}");
            }
        }

        // Message Templates (keep same as Twilio for compatibility)
        public string GetBookingConfirmation(string patientName, DateTime slotTime)
        {
            return $@"✅ Appointment Confirmed

Hello {patientName}!

Doctor: Dr. Sharma
Date: {slotTime:dddd, dd MMM yyyy}
Time: {slotTime:hh:mm tt} - {slotTime.AddMinutes(10):hh:mm tt}

Please arrive 10 minutes early.

Reply 'QUEUE' anytime to check your position.";
        }

        public string GetQueueUpdate(string doctorName, int position, int waitMinutes)
        {
            return $@"📋 *Queue Status Update*

👨‍⚕️ *Doctor:* {doctorName}
📍 *Current Position:* #{position}
⏳ *Estimated Wait:* approximately {waitMinutes} minutes

We'll notify you when the doctor is ready.";
        }

        public string GetDoctorReady(string patientName)
        {
            return $@"🔔 Doctor Ready!

Hello {patientName}!

The doctor is ready to see you now.
Please enter the consultation room.";
        }

        public string GetAppointmentMovedEarlier(string patientName, DateTime newSlotTime)
        {
            return $@"⚡ Good News!

Hello {patientName}!

Your appointment has been moved earlier to:
{newSlotTime:hh:mm tt}

Please arrive as soon as possible.";
        }

        public string GetArrivalReminder(string patientName, DateTime slotTime)
        {
            return $@"⏰ Reminder

Hello {patientName}!

Your appointment is in 1 hour at {slotTime:hh:mm tt}.

Please arrive 10 minutes early.";
        }

        public string GetArrivalConfirmation(string patientName, string doctorName, int queuePosition, int waitMinutes)
        {
            return $@"✅ *You are now checked in!*

👨‍⚕️ *Doctor:* {doctorName}
📍 *Your position in queue:* #{queuePosition}
⏳ *Estimated wait time:* ~{waitMinutes} minutes

Please stay nearby.
We’ll notify you when you’re next.";
        }

        public string GetNextInLineNotification(string doctorName)
        {
            return $@"⏰ *You're next!*

👨‍⚕️ *Doctor:* {doctorName}
📍 *Queue position:* #1

Your consultation will start in approximately 10 minutes.
Please be ready.";
        }

        public string GetNowServingNotification(string patientName)
        {
            return $@"🟢 It's Your Turn!

Please proceed to the consultation room now.";
        }

        public async Task SendYouAreNextNotificationAsync(string phone, string patientName, int position, int estimatedWaitMinutes)
        {
            var message = $@"🔔 *You're Up Next!*

Hi {patientName}, you're #{position} in the queue.

⏰ You'll be called in approximately *{estimatedWaitMinutes} minutes*.

Please make sure you're ready at the clinic.

_ClinicQueue_";

            await SendTextMessageAsync(phone, message);
        }

        public async Task SendArrivalReminderAsync(string phone, string patientName, DateTime slotTime)
        {
            var timeStr = slotTime.ToString("h:mm tt");
            
            // Use button message for better UX
            await SendButtonMessageAsync(
                phone,
                $@"⏰ *Appointment Reminder*

Hi {patientName}, your appointment is at *{timeStr}* (in 20 minutes).

We haven't marked you as arrived yet. Are you on your way?",
                new List<ButtonDto>
                {
                    new ButtonDto { Id = "yes", Title = "✅ Yes, coming" },
                    new ButtonDto { Id = "no", Title = "❌ Can't make it" }
                }
            );
        }

        // Response classes
        private class MetaApiResponse
        {
            public List<MessageInfo>? messages { get; set; }
        }

        private class MessageInfo
        {
            public string? id { get; set; }
        }
    }
}
