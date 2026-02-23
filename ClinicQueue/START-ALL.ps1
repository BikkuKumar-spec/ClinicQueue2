# Clinic Queue System - Complete Startup Script
# This opens 4 windows: Translation Service, Backend, Ngrok, and Frontend

Write-Host @"
╔═══════════════════════════════════════════════════════╗
║     🏥 CLINIC QUEUE SYSTEM - AUTO STARTUP 🏥          ║
╚═══════════════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

Write-Host "Starting all services in separate windows..." -ForegroundColor Green
Write-Host ""

# Get current directory
$projectDir = $PSScriptRoot

# 0. Start Translation Service
Write-Host "✅ Opening AI Translation Service (FastAPI)..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Title 'AI Translation Service'; & '$projectDir\start-translation.ps1'" -WorkingDirectory $projectDir

Start-Sleep -Seconds 5

# 1. Start Backend
Write-Host "✅ Opening .NET Backend..." -ForegroundColor Cyan
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Title 'ClinicQueue Backend'; dotnet run --project '$projectDir\ClinicQueue\ClinicQueue.csproj'" -WorkingDirectory $projectDir

Start-Sleep -Seconds 5

# 2. Start Ngrok
Write-Host "✅ Opening Ngrok Tunnel..." -ForegroundColor Blue
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Title 'Ngrok Tunnel'; & '$projectDir\start-ngrok.ps1'" -WorkingDirectory $projectDir

Start-Sleep -Seconds 2

# 3. Start Frontend
Write-Host "✅ Opening Frontend Dashboard (Vite)..." -ForegroundColor Magenta
Start-Process powershell -ArgumentList "-NoExit", "-Command", "Set-Title 'ClinicQueue Frontend'; & '$projectDir\start-frontend.ps1'" -WorkingDirectory $projectDir

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "🎉 ALL SERVICES STARTED!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Next Steps:" -ForegroundColor Yellow
Write-Host "   1. Wait for Backend to show 'Now listening on: http://localhost:5000'" -ForegroundColor White
Write-Host "   2. Copy the HTTPS URL from Ngrok window" -ForegroundColor White
Write-Host "   3. Update Twilio Webhook: https://console.twilio.com" -ForegroundColor White
Write-Host "      -> Paste: https://YOUR-URL.ngrok-free.dev/api/whatsapp/webhook" -ForegroundColor White
Write-Host "   4. Test WhatsApp by sending 'Hi'" -ForegroundColor White
Write-Host ""
Write-Host "Press any key to close this window..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
