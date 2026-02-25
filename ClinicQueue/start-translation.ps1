# Startup script for Translation Microservice
$projectDir = $PSScriptRoot
Set-Location "$projectDir\..\TranslationService"

Write-Host "Starting AI Translation Service..." -ForegroundColor Cyan
.\venv\Scripts\python.exe -m uvicorn main:app --port 5001
