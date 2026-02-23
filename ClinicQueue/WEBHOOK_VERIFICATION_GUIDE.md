# Meta WhatsApp Webhook Verification - Complete Guide

## 1. Why Meta Sends a GET Request

When you configure a webhook in Meta Business Suite, Meta **immediately** sends a **GET request** to verify your endpoint before saving the webhook URL.

### The Verification Flow:

```
Meta → GET https://your-domain/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=YOUR_TOKEN&hub.challenge=RANDOM_STRING

Your Server → Returns ONLY the hub.challenge value with 200 OK

Meta → ✅ Webhook verified and saved
```

### Why This Design?

1. **Security**: Ensures you control the endpoint (can't just point to someone else's server)
2. **Validation**: Confirms the endpoint is live and responding
3. **Token Matching**: Verifies you have the correct verify token (shared secret)
4. **Challenge-Response**: Prevents replay attacks and confirms real-time verification

---

## 2. Exact Backend Code (ASP.NET Core)

### ✅ CORRECT Implementation

```csharp
[ApiController]
[Route("api/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IConfiguration configuration,
        ILogger<WhatsAppWebhookController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// GET endpoint - Meta webhook verification
    /// Meta calls this ONCE when you configure the webhook
    /// </summary>
    [HttpGet("webhook")]
    public IActionResult VerifyWebhook(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? token,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        // Get your verify token from configuration
        var expectedToken = _configuration["Meta:WhatsApp:WebhookVerifyToken"];
        
        _logger.LogInformation($"Webhook verification attempt: mode={mode}, token={token}");

        // Check all three conditions
        if (mode == "subscribe" && 
            token == expectedToken && 
            !string.IsNullOrEmpty(challenge))
        {
            _logger.LogInformation("✅ Webhook verified successfully!");
            
            // CRITICAL: Return ONLY the challenge string as plain text
            return Ok(challenge);
        }

        _logger.LogWarning("❌ Webhook verification FAILED");
        return Forbid();
    }

    /// <summary>
    /// POST endpoint - Receive actual messages
    /// Meta calls this for EVERY incoming message
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveMessage()
    {
        // Your message handling logic here
        // Parse JSON, extract message, send to bot service, etc.
        
        // ALWAYS return 200 OK immediately
        return Ok();
    }
}
```

### Key Points:

1. **Two Separate Endpoints**:
   - `[HttpGet("webhook")]` - Verification (called once during setup)
   - `[HttpPost("webhook")]` - Messages (called for every message)

2. **Query Parameter Names**: Use `[FromQuery(Name = "hub.mode")]` because parameter names have dots

3. **Return Value**: `return Ok(challenge)` returns 200 with challenge string as body

4. **All Three Checks**:
   - `mode == "subscribe"`
   - `token == expectedToken` (exact match, case-sensitive)
   - `challenge` is not null/empty

---

## 3. Common Mistakes That Cause This Error

### ❌ Mistake #1: Wrong Return Format
```csharp
// WRONG - Returns JSON
return Ok(new { challenge });

// CORRECT - Returns plain text
return Ok(challenge);
```

### ❌ Mistake #2: Token Mismatch
```
appsettings.json: "WebhookVerifyToken": "ClinicQueue_Webhook_Secret_2026"
Meta Dashboard:   "ClinicQueue_Webhook_Secret_2026 "  ← Extra space!
```
**Solution**: Copy-paste exact token, check for trailing spaces

### ❌ Mistake #3: Backend Not Running
```bash
# Test returns nothing
curl "http://localhost:5000/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=XXX&hub.challenge=test"

# Backend is stopped!
```
**Solution**: Run `dotnet run` and keep it running

### ❌ Mistake #4: ngrok URL Changed
```
Old: https://abc123.ngrok-free.dev
New: https://xyz789.ngrok-free.dev  ← Restarted ngrok!
```
**Solution**: Use the current ngrok URL in Meta

### ❌ Mistake #5: Checking POST Instead of GET
```csharp
// WRONG - Only has POST
[HttpPost("webhook")]

// CORRECT - Has BOTH
[HttpGet("webhook")]  // For verification
[HttpPost("webhook")] // For messages
```

### ❌ Mistake #6: Wrong Route
```
Meta webhook: /api/whatsapp/webhook
Your route: /api/webhook  ← Missing 'whatsapp'!
```

### ❌ Mistake #7: HTTPS Required
```
Meta webhook: http://example.com/webhook  ← HTTP not allowed!
Correct:      https://example.com/webhook
```

### ❌ Mistake #8: Firewall/CORS Blocking
- ngrok blocked by organization firewall
- Backend not accessible from internet
- Port 5000 not forwarded

---

## 4. Client Certificates - NOT Required

**Answer: NO, client certificates are NOT required for ngrok + WhatsApp.**

### What You Need:
- ✅ HTTPS URL (ngrok provides this automatically)
- ✅ Valid webhook endpoint responding to GET requests
- ✅ Matching verify token

### What You DON'T Need:
- ❌ Client certificates
- ❌ SSL certificates (ngrok handles this)
- ❌ Domain verification
- ❌ Static IP address

**Note**: Client certificates are optional and only for advanced security. Meta supports them but doesn't require them.

---

## 5. Quick Verification Checklist

### ✅ Pre-Configuration Checks

```bash
# 1. Check backend is running
netstat -ano | findstr :5000
# Should show: TCP 0.0.0.0:5000 ... LISTENING

# 2. Test local endpoint
curl "http://localhost:5000/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=TESTCHALLENGE123"
# Should return: TESTCHALLENGE123

# 3. Test via ngrok
curl "https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=TESTCHALLENGE123"
# Should return: TESTCHALLENGE123

# 4. Check ngrok is running
curl https://unallegorical-lauditorily-elliot.ngrok-free.dev
# Should NOT return "404" or connection error
```

### ✅ Configuration Checks

- [ ] Backend running (`dotnet run`)
- [ ] ngrok running on port 5000
- [ ] ngrok URL is HTTPS
- [ ] Verify token in `appsettings.json` matches Meta exactly
- [ ] No extra spaces in verify token
- [ ] Case matches exactly
- [ ] Route is `/api/whatsapp/webhook`
- [ ] GET endpoint exists (not just POST)
- [ ] Returns plain string (not JSON)

### ✅ Meta Dashboard Checks

- [ ] Callback URL: `https://YOUR_NGROK_URL/api/whatsapp/webhook`
- [ ] Verify token matches appsettings.json exactly
- [ ] App is in Development mode (for testing)
- [ ] No typos in URL

---

## 6. Debugging Steps

### Step 1: Check Your Backend Logs

When Meta tries to verify, you should see:
```
[INFO] Webhook verification attempt: mode=subscribe, token=ClinicQueue_Webhook_Secret_2026
[INFO] ✅ Webhook verified successfully!
```

If you don't see this, backend isn't receiving the request.

### Step 2: Test Manually

```bash
# This simulates EXACTLY what Meta does
curl -v "https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=TEST123"
```

**Expected Response:**
```
HTTP/1.1 200 OK
Content-Type: text/plain

TEST123
```

**Not This:**
```json
{"challenge": "TEST123"}  ← Wrong! This is JSON
```

### Step 3: Check Token Exactly

In your terminal:
```bash
# Check exact token from config
dotnet user-secrets list
# Or view appsettings.json directly
```

Copy this EXACT value to Meta (no manual typing).

### Step 4: Verify URL Structure

```
✅ CORRECT:
https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook

❌ WRONG:
http://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook  ← HTTP
https://unallegorical-lauditorily-elliot.ngrok-free.dev/webhook  ← Missing /api/whatsapp
https://localhost:5000/api/whatsapp/webhook  ← Not accessible from internet
```

---

## 7. Your Current Setup

**Callback URL:**
```
https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook
```

**Verify Token:**
```
ClinicQueue_Webhook_Secret_2026
```

**Test Command:**
```bash
curl "https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=VERIFY123"
```

**Expected:** `VERIFY123`

---

## 8. Final Checklist Before Configuring in Meta

Run these commands in order:

```bash
# 1. Start backend (if not running)
cd c:\Metahelp\ClinicQueue
dotnet run

# 2. In another terminal, test local
curl "http://localhost:5000/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=LOCAL_TEST"
# Must return: LOCAL_TEST

# 3. Test via ngrok
curl "https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=NGROK_TEST"
# Must return: NGROK_TEST

# 4. If both work, configure in Meta with:
# URL: https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook
# Token: ClinicQueue_Webhook_Secret_2026
```

---

**If all tests pass but Meta still fails, the issue is likely:**
1. Token mismatch (extra space, wrong case)
2. ngrok URL changed (restarted ngrok)
3. Meta caching old configuration (wait 1 minute and retry)

Let me know the results of the curl tests!
