# Quick Fix: Network Error

## Most Common Issue: Backend Not Running

```powershell
# Start backend
cd c:\Metahelp\ClinicQueue
dotnet run
```

Wait for: `Now listening on: http://localhost:5000`

## Next: Restart Frontend

```powershell
# Stop frontend (Ctrl+C)
# Then restart:
cd c:\Metahelp\ClinicQueue\frontend
npm run dev
```

## Test

1. Open `http://localhost:5173`
2. Login
3. Click "Mark Arrived"
4. Press F12, check Console tab for `[Dashboard]` logs

The error message will now tell you exactly what's wrong!

**See NETWORK_ERROR_TROUBLESHOOTING.md for full guide.**
