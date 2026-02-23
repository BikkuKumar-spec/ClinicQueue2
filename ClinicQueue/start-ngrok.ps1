# Start Ngrok Tunnel
Write-Host "🌐 Starting Ngrok Tunnel..." -ForegroundColor Cyan
Write-Host "Backend must be running on port 5000" -ForegroundColor Yellow
Write-Host "Copy the HTTPS URL and update Twilio webhook" -ForegroundColor Yellow
Write-Host ""

.\ngrok http 5000
