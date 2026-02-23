# Meta WhatsApp Cloud API - Setup Guide

## 🎯 Overview

Your WhatsApp chatbot has been successfully migrated from Twilio to Meta WhatsApp Cloud API! This guide will help you complete the setup and get your bot running.

## 📋 Prerequisites Checklist

Before you can use the bot, you need to obtain these credentials from Meta:

### Required Credentials

1. **Phone Number ID** - Your WhatsApp Business phone number identifier
2. **Business Account ID** - Your WhatsApp Business Account ID
3. **Access Token** - Permanent access token (already provided ✅)
4. **Webhook Verify Token** - A custom secret string you create
5. **App Secret** - For webhook signature validation

> [!IMPORTANT]
> **Where to Get These Credentials**
> - Log into [Meta Business Suite](https://business.facebook.com/)
> - Go to WhatsApp Manager → API Setup
> - Find your Phone Number ID and Business Account ID
> - Generate a permanent access token (System User token recommended)
> - Find App Secret in App Dashboard → Settings → Basic

## ⚙️ Configuration Steps

### Step 1: Update appsettings.json

Open `appsettings.json` and replace the placeholder values:

```json
"Meta": {
  "WhatsApp": {
    "PhoneNumberId": "YOUR_ACTUAL_PHONE_NUMBER_ID",
    "BusinessAccountId": "YOUR_ACTUAL_BUSINESS_ACCOUNT_ID",
    "AccessToken": "EAAWMPrm6T5ABQlkxIVkUv2v3GLKZCokqTRDLUZC1nZAbNbcSBTKzAzIBdHge3wHYf99rZCoMsaZCjnDIf8zJhP1PyyBOvaxFRpQ2jsrP04k5WXdlFpYBIU2yIJ8ZAmFTfLtsBvg6FYqAGzNOfqgtPsvRyMeQwSsoKpju5oCkh98VmrbSIm9t7OBHzbpVKeaDMOGrrVB2NZAyDsVlFgsPicatJMduf2ZAk9GWFd9YCnevzYZCPCvubgXZCD3pNqhIzQZCKaL44x6ZCCZAKkD9172G4V10ZBhlXf",
    "WebhookVerifyToken": "MY_CUSTOM_SECRET_TOKEN_123",
    "AppSecret": "YOUR_APP_SECRET_FROM_META",
    "ApiVersion": "v21.0"
  }
}
```

**How to fill these:**

| Field | What to Do |
|-------|-----------|
| `PhoneNumberId` | Copy from Meta Business Suite → WhatsApp → API Setup |
| `BusinessAccountId` | Copy from Meta Business Suite → WhatsApp → Settings |
| `AccessToken` | Already configured ✅ (or generate new permanent token) |
| `WebhookVerifyToken` | **CREATE YOUR OWN** - Any random string (e.g., `MySecretToken123!`) |
| `AppSecret` | Copy from Meta App Dashboard → Settings → Basic |

### Step 2: Configure Webhook in Meta

Once your backend is running, you need to tell Meta where to send messages.

1. **Start your backend** (with ngrok if testing locally)
   ```bash
   dotnet run
   ```

2. **Expose via ngrok** (if testing locally)
   ```bash
   ngrok http 5000
   ```
   Note the HTTPS URL (e.g., `https://abc123.ngrok.io`)

3. **Configure in Meta Business Suite**
   - Go to App Dashboard → WhatsApp → Configuration
   - Click "Edit" next to Webhook
   - **Callback URL**: `https://YOUR_DOMAIN/api/whatsapp/webhook`
   - **Verify Token**: Enter the SAME token you put in `WebhookVerifyToken`
   - Click "Verify and Save"

4. **Subscribe to Webhook Fields**
   - Check: `messages` (required for receiving messages)
   - Click "Subscribe"

### Step 3: Test the Webhook

Verify webhook is working:

```bash
# Test GET endpoint (webhook verification)
curl "https://YOUR_DOMAIN/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=MY_CUSTOM_SECRET_TOKEN_123&hub.challenge=test123"

# Expected response: "test123"
```

## 🚀 Running the Application

### Development

```bash
cd c:\Metahelp\ClinicQueue
dotnet run
```

### Production

Make sure to:
- Use environment variables for sensitive data (Access Token, App Secret)
- Use a proper production URL (not ngrok)
- Enable HTTPS
- Configure firewall rules

## 📱 Testing the Bot

### 1. Send Your First Message

Send a WhatsApp message to your business number:

```
BOOK
```

**Expected Behavior:**
- Bot should respond with available time slots
- You can select a slot by replying with the number

### 2. Check Queue Position

```
QUEUE
```

**Expected Behavior:**
- Shows your current appointment status
- If arrived, shows queue position and estimated wait time

### 3. Interactive Messages

The bot now supports:
- ✅ **Button messages** - For Yes/No confirmations
- 📋 **List messages** - For slot selection (optional upgrade)
- 💬 **Text messages** - For names and general responses

## 🔍 Troubleshooting

### Messages Not Being Received

**Check:**
1. Webhook URL is correct in Meta settings
2. Verify token matches in both appsettings.json and Meta
3. Backend is running and accessible
4. Check backend logs for errors

```bash
# View logs
dotnet run --verbosity detailed
```

### Messages Not Being Sent

**Check:**
1. Access Token is valid and not expired
2. Phone Number ID is correct
3. Check Meta API status: https://developers.facebook.com/status/
4. Verify you have message credits/billing enabled

**View Response in Logs:**
```
[INFO] Sending to Meta API: {...}
[INFO] Meta API Response: {...}
```

### Webhook Signature Validation Failures

**Issue:** Meta rejects your responses with 401 Unauthorized

**Fix:**
- Verify App Secret is correct in appsettings.json
- Check logs for signature mismatch errors
- Temporarily disable validation for testing (not recommended for production)

### Rate Limiting

Meta has stricter rate limits than Twilio:
- **80 messages/second** per phone number
- **1000 conversations** per rolling 24-hour period (may vary by tier)

**Solution:** Implement retry logic and backoff strategies

## 📊 Database & Logging

All messages are logged to the `notification_log` table:

```sql
SELECT * FROM notification_log 
ORDER BY sent_at DESC 
LIMIT 10;
```

**Columns:**
- `phone` - Recipient phone number
- `message` - Message content
- `status` - sent/failed
- `sent_at` - Timestamp

## 🎨 Enhanced Features (Optional)

### Enable List Messages for Slot Selection

Currently, slots are shown as numbered text. You can upgrade to interactive list messages:

**In [WhatsAppBotService.cs](file:///c:/Metahelp/ClinicQueue/Services/WhatsAppBotService.cs)**, find `ShowAvailableSlots()` and replace the text response with:

```csharp
var sections = new List<ListSectionDto>
{
    new ListSectionDto
    {
        Title = "Available Slots",
        Rows = availableSlots.Take(10).Select((slot, i) => new ListRowDto
        {
            Id = $"slot_{i}",
            Title = $"{slot.BatchStartTime:h:mm tt}",
            Description = $"{slot.MaxBookingsPerBatch - slot.BookingsInBatch} spots left"
        }).ToList()
    }
};

var messageId = await _metaService.SendListMessageAsync(
    phoneNumber,
    "📅 Available Time Slots\n\nSelect your preferred appointment time:",
    "Select Slot",
    sections,
    "Dr. Sharma's Clinic"
);
```

### Enable Button Messages for Commands

Replace the main menu text with buttons:

```csharp
await _metaService.SendButtonMessageAsync(
    phoneNumber,
    "🏥 Dr. Sharma's Clinic\n\nHow can I help you today?",
    new List<ButtonDto>
    {
        new ButtonDto { Id = "book", Title = "📅 Book" },
        new ButtonDto { Id = "queue", Title = "🔢 Queue" }
    },
    "Welcome!"
);
```

## 🔐 Security Best Practices

1. **Never commit credentials** to version control
   - Use environment variables in production
   - Add `appsettings.Production.json` to `.gitignore`

2. **Always validate webhook signatures**
   - Ensure `AppSecret` is configured
   - Check logs for validation failures

3. **Use HTTPS** in production
   - Meta requires HTTPS for webhooks
   - Use SSL certificates

4. **Rotate tokens periodically**
   - Generate new access tokens every 60-90 days
   - Update in configuration

## 📚 Additional Resources

- [Meta WhatsApp Cloud API Docs](https://developers.facebook.com/docs/whatsapp/cloud-api)
- [Interactive Messages Guide](https://developers.facebook.com/docs/whatsapp/cloud-api/guides/send-messages#interactive-messages)
- [Webhook Setup Guide](https://developers.facebook.com/docs/whatsapp/cloud-api/guides/set-up-webhooks)
- [Error Codes Reference](https://developers.facebook.com/docs/whatsapp/cloud-api/support/error-codes)

## ✅ Quick Start Checklist

- [ ] Updated `PhoneNumberId` in appsettings.json
- [ ] Updated `BusinessAccountId` in appsettings.json
- [ ] Created custom `WebhookVerifyToken`
- [ ] Added `AppSecret` from Meta dashboard
- [ ] Started backend with `dotnet run`
- [ ] Configured webhook URL in Meta Business Suite
- [ ] Verified webhook with test challenge
- [ ] Subscribed to `messages` webhook field
- [ ] Sent test message "BOOK" to WhatsApp number
- [ ] Verified bot responds with available slots

---

**Need Help?** Check the logs for detailed error messages and refer to the troubleshooting section above.
