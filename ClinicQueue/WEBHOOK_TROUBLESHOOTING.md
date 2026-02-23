# 🔧 Webhook Verification Troubleshooting

## ❌ Problem
Meta cannot verify your webhook with the error:
> "The callback URL or verify token couldn't be validated."

## 🔍 Diagnosis

**Backend Status:** ❌ NOT RUNNING
- Port 5000 is not listening
- This is why Meta cannot reach your webhook

## ✅ Solution Steps

### 1. Start Your Backend

Open a **new terminal** in your project directory and run:

```powershell
cd c:\Metahelp\ClinicQueue
dotnet run
```

**Expected output:**
```
Building...
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

✅ **Keep this terminal running!** Don't close it.

---

### 2. Verify Backend is Running

Open **another terminal** and test:

```powershell
curl "http://localhost:5000/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=TESTCHALLENGE"
```

**Expected Response:** `TESTCHALLENGE`

If you see this, your backend is working! ✅

---

### 3. Test Through ngrok

```powershell
curl "https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=TESTCHALLENGE"
```

**Expected Response:** `TESTCHALLENGE`

---

### 4. Configure Webhook in Meta Again

Now that your backend is running, go back to Meta and try again:

1. Go to https://developers.facebook.com/apps
2. Select your WhatsApp app
3. Click **WhatsApp** → **Configuration**
4. Edit Webhook settings:
   - **Callback URL**: `https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook`
   - **Verify Token**: `ClinicQueue_Webhook_Secret_2026`
5. Click **Verify and Save**

This time it should work! ✅

---

### 5. Subscribe to Messages

After successful verification:
- Find the **Webhook fields** section
- Check the box next to **messages**
- Click **Subscribe**

---

## 🎯 Quick Checklist

Before configuring webhook in Meta, verify:

- [ ] Backend is running (`dotnet run`)
- [ ] Logs show "Now listening on: http://localhost:5000"
- [ ] ngrok is running and showing the same URL
- [ ] Local test returns "TESTCHALLENGE"
- [ ] ngrok URL test returns "TESTCHALLENGE"
- [ ] WebhookVerifyToken matches in both places

---

## 🔄 Common Issues

### Issue: ngrok URL changed
**Solution:** 
- Note the new ngrok URL
- Update webhook URL in Meta with the new ngrok URL

### Issue: Backend crashes or stops
**Solution:**
- Check terminal for error messages
- Restart with `dotnet run`
- Check if port 5000 is already in use

### Issue: "Forbidden" response
**Solution:**
- Verify token mismatch
- Make sure `ClinicQueue_Webhook_Secret_2026` is exactly the same in both places
- Case-sensitive!

### Issue: ngrok shows "502 Bad Gateway"
**Solution:**
- Backend is not running
- Start backend with `dotnet run`

---

## 📊 Monitoring

Once webhook is configured, watch your backend terminal for incoming webhooks:

```
[INFO] Webhook verification request: mode=subscribe, token=ClinicQueue_Webhook_Secret_2026
[INFO] Webhook verified successfully
```

---

## 🚀 After Successful Verification

1. **Send test message** to +1 555 162 0464:
   ```
   BOOK
   ```

2. **Watch backend logs:**
   ```
   [INFO] Received webhook: {...}
   [INFO] Processing message from 15551620464: BOOK
   [INFO] Sending to Meta API: {...}
   ```

3. **Check WhatsApp** for bot response with available slots!

---

## ⚡ Quick Commands Reference

```powershell
# Start backend
cd c:\Metahelp\ClinicQueue
dotnet run

# Test local webhook (in another terminal)
curl "http://localhost:5000/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=TEST"

# Test via ngrok
curl "https://unallegorical-lauditorily-elliot.ngrok-free.dev/api/whatsapp/webhook?hub.mode=subscribe&hub.verify_token=ClinicQueue_Webhook_Secret_2026&hub.challenge=TEST"

# Check if port 5000 is listening
netstat -ano | findstr :5000
```

---

**Start your backend now and try configuring the webhook again!** 🎉
