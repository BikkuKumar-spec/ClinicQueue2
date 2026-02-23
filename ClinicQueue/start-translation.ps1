# Startup script for Translation Microservice
$projectDir = $PSScriptRoot
Set-Location "$projectDir\TranslationService"

Write-Host "Starting AI Translation Service..." -ForegroundColor Cyan
python main.py
