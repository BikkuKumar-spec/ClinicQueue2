@echo off
echo ========================================
echo   CLINIC QUEUE - SERVICE STARTER
echo ========================================
echo.
echo Killing old processes...
taskkill /F /IM ClinicQueue.exe 2>nul
taskkill /F /IM ngrok.exe 2>nul
timeout /t 2 /nobreak >nul

echo.
echo Starting Backend Server...
start "Clinic Backend" cmd /k "cd /d %~dp0 && dotnet run"

echo Waiting for backend to start...
timeout /t 5 /nobreak >nul

echo.
echo Starting Ngrok Tunnel...
start "Ngrok Tunnel" cmd /k "cd /d %~dp0 && ngrok http 5000"

echo.
echo ========================================
echo   ALL SERVICES STARTED!
echo ========================================
echo.
echo NEXT STEPS:
echo 1. Wait for Backend window: "Now listening on: http://localhost:5000"
echo 2. Copy Ngrok HTTPS URL from Ngrok window
echo 3. Update Twilio: https://console.twilio.com
echo    Paste: YOUR-NGROK-URL/api/whatsapp/webhook
echo 4. Test WhatsApp by sending "Hi"
echo.
echo Press any key to close this window...
pause >nul
