using Microsoft.AspNetCore.Mvc;
using ClinicQueue.Services;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Serialization;

namespace ClinicQueue.Controllers
{
    [ApiController]
    [Route("api/whatsapp")]
    public class WhatsAppWebhookController : ControllerBase
    {
        private readonly ILogger<WhatsAppWebhookController> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _configuration;
        private readonly string _verifyToken;
        private readonly string _appSecret;

        public WhatsAppWebhookController(
            ILogger<WhatsAppWebhookController> logger,
            IServiceScopeFactory scopeFactory,
            IConfiguration configuration)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _configuration = configuration;
            _verifyToken = configuration["Meta:WhatsApp:WebhookVerifyToken"] ?? "VERIFY_TOKEN";
            _appSecret = configuration["Meta:WhatsApp:AppSecret"] ?? "";
        }

        // =================== GET: Webhook verification ===================

        [HttpGet]
        [HttpGet("webhook")]
        public IActionResult VerifyWebhook(
            [FromQuery(Name = "hub.mode")] string? mode,
            [FromQuery(Name = "hub.verify_token")] string? token,
            [FromQuery(Name = "hub.challenge")] string? challenge)
        {
            _logger.LogInformation("Webhook verification request received");

            if (mode == "subscribe" && token == _verifyToken)
            {
                return Ok(challenge);
            }

            return Forbid();
        }

        // =================== POST: Receive WhatsApp messages ===================

        [HttpPost]
        [HttpPost("webhook")]
        public async Task<IActionResult> ReceiveMessage()
        {
            try
            {
                string body;
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    body = await reader.ReadToEndAsync();
                }

                _logger.LogInformation("Incoming webhook: {body}", body);

                var payload = JsonSerializer.Deserialize<MetaWebhookPayload>(
                    body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );

                if (payload?.Entry == null)
                    return Ok();

                foreach (var entry in payload.Entry)
                {
                    if (entry.Changes == null) continue;

                    foreach (var change in entry.Changes)
                    {
                        var messages = change.Value?.Messages;
                        if (messages == null) continue;

                        foreach (var message in messages)
                        {
                            var from = message.From;
                            var input = ExtractUserInput(message);

                            _logger.LogInformation(
                                "Message type={type}, extracted input={input}",
                                message.Type,
                                input
                            );

                            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(input))
                                continue;

                            // ✅ Capture loop variables for the closure
                            var capturedFrom  = from;
                            var capturedInput = input;

                            // ✅ Per-user serialized processing.
                            // Only ONE message per phone number runs at a time.
                            // If a Llama response takes 25s and the user presses a button
                            // during that time, the button is queued and processed AFTER
                            // Llama finishes — session state is never clobbered.
                            _ = Task.Run(async () =>
                            {
                                using var scope = _scopeFactory.CreateScope();
                                var sessions = scope.ServiceProvider.GetRequiredService<BotSessionStore>();
                                var phoneLock = sessions.GetLock(capturedFrom);

                                await phoneLock.WaitAsync();
                                try
                                {
                                    var bot = scope.ServiceProvider.GetRequiredService<WhatsAppBotService>();
                                    await bot.HandleMessageAsync(capturedFrom, capturedInput);
                                }
                                finally
                                {
                                    phoneLock.Release();
                                }
                            });
                        }
                    }
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook error");
                return Ok();
            }
        }

        private string ExtractUserInput(MetaMessage message)
        {
            if (message == null) return "";

            return message.Type switch
            {
                "text" => message.Text?.Body?.Trim() ?? "",

                "interactive" when message.Interactive?.Type == "button_reply"
                    => message.Interactive.ButtonReply?.Id ?? "",

                "interactive" when message.Interactive?.Type == "list_reply"
                    => message.Interactive.ListReply?.Id ?? "",

                _ => ""
            };
        }

        private bool ValidateSignature(string payload, string? signature)
        {
            if (string.IsNullOrEmpty(signature)) return false;
            var expected = "sha256=" + ComputeHmacSha256(_appSecret, payload);
            return signature == expected;
        }

        private string ComputeHmacSha256(string secret, string payload)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    // =================== MODELS ===================

    public class MetaWebhookPayload
    {
        public List<MetaEntry>? Entry { get; set; }
    }

    public class MetaEntry
    {
        public List<MetaChange>? Changes { get; set; }
    }

    public class MetaChange
    {
        public MetaValue? Value { get; set; }
    }

    public class MetaValue
    {
        public List<MetaMessage>? Messages { get; set; }
    }

    public class MetaMessage
    {
        public string? From { get; set; }
        public string? Type { get; set; }
        public MetaText? Text { get; set; }
        public MetaInteractive? Interactive { get; set; }
    }

    public class MetaText
    {
        public string? Body { get; set; }
    }

    public class MetaInteractive
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("button_reply")]
        public MetaButtonReply? ButtonReply { get; set; }

        [JsonPropertyName("list_reply")]
        public MetaListReply? ListReply { get; set; }
    }

    public class MetaButtonReply
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    public class MetaListReply
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }
}

