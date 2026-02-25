# Start Frontend Development Server
Write-Host "⚛️ Starting React Frontend..." -ForegroundColor Magenta
Write-Host "Dashboard will open at http://localhost:5173" -ForegroundColor Yellow
Write-Host ""

Set-Location frontend
cmd.exe /c "npm run dev"
