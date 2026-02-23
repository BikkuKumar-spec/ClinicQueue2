# 🔍 Network Error Troubleshooting Guide

## Problem: "Failed to update status - network error"

This error means the frontend cannot communicate with the backend API.

---

## ✅ Quick Checklist

Run through these steps in order:

### 1. **Is the Backend Running?**

```powershell
# Check if backend is running on port 5000
netstat -ano | findstr :5000
```

**Expected:** You should see a line with `:5000` and `LISTENING`

**If empty:** Backend is NOT running. Start it:
```powershell
cd c:\Metahelp\ClinicQueue
dotnet run
```

Wait for: `Now listening on: http://localhost:5000`

---

### 2. **Is the Frontend Running?**

```powershell
# Frontend should be on port 5173
netstat -ano | findstr :5173
```

**Expected:** You should see `:5173` and `LISTENING`

**If empty:** Start frontend:
```powershell
cd c:\Metahelp\ClinicQueue\frontend
npm run dev
```

Wait for: `Local: http://localhost:5173`

---

### 3. **Test Backend API Directly**

```powershell
# Test if backend responds
curl http://localhost:5000/api/dashboard/appointments/today -H "Authorization: Bearer YOUR_TOKEN"
```

**Expected:** JSON response with appointments

**If fails:** 
- Check backend console for errors
- Verify JWT token is valid
- Check if database file exists

---

### 4. **Check Browser Console**

1. Open Dashboard in browser: `http://localhost:5173`
2. Press `F12` to open DevTools
3. Go to **Console** tab
4. Click "Mark Arrived"
5. Look for error messages

**Common errors:**

**"ERR_CONNECTION_REFUSED"**
- Backend not running
- Wrong port (should be 5000)

**"401 Unauthorized"**
- Token expired
- Try logging out and back in

**"CORS error"**
- Backend CORS misconfigured
- Check Program.cs CORS policy

**"404 Not Found"**
- Wrong API endpoint URL
- Check route in controller

---

### 5. **Check Network Tab**

1. In DevTools, go to **Network** tab
2. Click "Mark Arrived"
3. Find the PATCH request to `/api/dashboard/appointments/…/arrive`
4. Click on it to see details

**Status 200:** Success - check response body
**Status 401:** Authentication issue
**Status 500:** Backend error - check backend console
**Failed/Cancelled:** Network issue

---

## 🔧 Common Fixes

### Fix 1: Restart Both Backend and Frontend

```powershell
# Terminal 1: Backend
cd c:\Metahelp\ClinicQueue
dotnet run

# Terminal 2: Frontend
cd c:\Metahelp\ClinicQueue\frontend
npm run dev
```

---

### Fix 2: Clear Browser Cache

1. Press `Ctrl + Shift + Delete`
2. Select "Cached images and files"
3. Click "Clear data"
4. Refresh page (`Ctrl + R`)

---

### Fix 3: Get New Auth Token

1. Logout from dashboard
2. Login again
3. New token will be generated
4. Try "Mark Arrived" again

---

### Fix 4: Check Vite Proxy Configuration

Verify `frontend/vite.config.js`:
```javascript
proxy: {
  '/api': {
    target: 'http://localhost:5000',
    changeOrigin: true,
    secure: false,
  },
  '/hubs': {
    target: 'http://localhost:5000',
    changeOrigin: true,
    secure: false,
    ws: true,
  },
}
```

---

### Fix 5: Verify Backend CORS

Check `Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Later in file:
app.UseCors("AllowReactApp");
```

---

## 📊 Detailed Error Messages

With the updated code, you'll now see more specific errors:

### Error: "No response from server. Is the backend running?"
**Cause:** Backend is not running or unreachable
**Fix:** Start backend with `dotnet run`

### Error: "Update failed: Unauthorized (Status: 401)"
**Cause:** Invalid or expired JWT token
**Fix:** Logout and login again

### Error: "Update failed: Internal Server Error (Status: 500)"
**Cause:** Backend threw an exception
**Fix:** Check backend console for stack trace

### Error: "Failed to mark patient as arrived: [error details]"
**Cause:** Business logic error (e.g., patient not found)
**Fix:** Check the error message for specific details

---

## 🧪 Testing the Fix

1. **Open browser console** (F12)
2. Click "Mark Arrived"
3. **Check console output:**

```
[Dashboard] Updating status: arrive for appointment abc-123
[Dashboard] Response: { success: true, message: "Patient marked as arrived" }
[Dashboard] Status update successful: Patient marked as arrived
```

**If successful:** No alert, status updates immediately

**If error:**
```
[Dashboard] Backend returned error: Patient not found
[Dashboard] // Alert shown with error message
```

---

## 🚀 Complete Startup Sequence

Follow these steps in order every time:

```powershell
# 1. Start Backend (Terminal 1)
cd c:\Metahelp\ClinicQueue
dotnet run
# Wait for: "Now listening on: http://localhost:5000"

# 2. Start Frontend (Terminal 2)
cd c:\Metahelp\ClinicQueue\frontend  
npm run dev
# Wait for: "Local: http://localhost:5173"

# 3. Open browser
# Navigate to: http://localhost:5173

# 4. Login with credentials

# 5. Test "Mark Arrived"
```

---

## 🔍 Debug Logs

The updated code now logs:
- Request being sent
- Response received
- Errors at each stage

**To view logs:**
1. Open browser console (F12)
2. Filter by `[Dashboard]`
3. See detailed request/response/error info

---

## ✅ Success Indicators

When everything works correctly:

1. ✅ Backend console shows: `Now listening on: http://localhost:5000`
2. ✅ Frontend console shows: `Local: http://localhost:5173`
3. ✅ Browser console shows: `[Dashboard] Status update successful`
4. ✅ No error alerts
5. ✅ Status changes immediately
6. ✅ Patient appears in queue

---

## 🆘 Still Not Working?

Share these details:

1. **Backend console output** (copy last 20 lines)
2. **Browser console errors** (screenshot)
3. **Network tab** (screenshot of failed request)
4. **Which step fails** in the checklist above

This will help diagnose the exact issue! 🔍
