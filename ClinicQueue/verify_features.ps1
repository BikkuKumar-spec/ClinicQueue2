$base = 'http://localhost:5000'
$lines = @()

try {
  $r = Invoke-WebRequest -Uri "$base/api/translation/detect" -Method POST -ContentType 'application/json' -Body '{"text":"नमस्ते"}' -UseBasicParsing -ErrorAction Stop
  $lines += "TRANSLATE_DETECT_STATUS=$($r.StatusCode)"
  $lines += "TRANSLATE_DETECT_BODY=$($r.Content)"
} catch {
  if ($_.Exception.Response) {
    $lines += "TRANSLATE_DETECT_STATUS=$([int]$_.Exception.Response.StatusCode)"
  } else {
    $lines += 'TRANSLATE_DETECT_STATUS=ERROR'
    $lines += $_.Exception.Message
  }
}

try {
  $r = Invoke-WebRequest -Uri "$base/api/translation" -Method POST -ContentType 'application/json' -Body '{"text":"hello","srcLang":"eng_Latn","targetLang":"hin_Deva"}' -UseBasicParsing -ErrorAction Stop
  $lines += "TRANSLATE_STATUS=$($r.StatusCode)"
  $lines += "TRANSLATE_BODY=$($r.Content)"
} catch {
  if ($_.Exception.Response) {
    $lines += "TRANSLATE_STATUS=$([int]$_.Exception.Response.StatusCode)"
  } else {
    $lines += 'TRANSLATE_STATUS=ERROR'
    $lines += $_.Exception.Message
  }
}

try {
  $r = Invoke-WebRequest -Uri 'http://localhost:5001/health' -UseBasicParsing -ErrorAction Stop
  $lines += "TRANSLATION_SERVICE_HEALTH=$($r.StatusCode)"
} catch {
  $lines += 'TRANSLATION_SERVICE_HEALTH=ERROR'
}

try {
  $r = Invoke-WebRequest -Uri 'http://localhost:8001/health' -UseBasicParsing -ErrorAction Stop
  $lines += "OCR_SERVICE_HEALTH=$($r.StatusCode)"
} catch {
  $lines += 'OCR_SERVICE_HEALTH=ERROR'
}

$img = 'c:/ClinicQueueFinal/ClinicQueue2/OCRService/venv/Lib/site-packages/skimage/data/text.png'
$ocrRaw = curl.exe -s -w "`n%{http_code}" -F "file=@$img" "$base/api/upload/extract-text"
$ocrParts = $ocrRaw -split "`n"
$lines += "OCR_UPLOAD_STATUS=$($ocrParts[-1])"
$lines += "OCR_UPLOAD_BODY=$($ocrParts[0])"

$pdf = 'c:/ClinicQueueFinal/ClinicQueue2/OCRService/venv/Lib/site-packages/matplotlib/mpl-data/images/home.pdf'
$pdfRaw = curl.exe -s -w "`n%{http_code}" -F "file=@$pdf" "$base/api/upload/extract-text"
$pdfParts = $pdfRaw -split "`n"
$lines += "PDF_UPLOAD_STATUS=$($pdfParts[-1])"
$lines += "PDF_UPLOAD_BODY=$($pdfParts[0])"

try {
  $payload = '{"object":"whatsapp_business_account","entry":[{"id":"WABA_ID","changes":[{"field":"messages","value":{"messaging_product":"whatsapp","messages":[{"from":"919999999999","type":"text","text":{"body":"hello"}}]}}]}]}'
  $r = Invoke-WebRequest -Uri "$base/api/whatsapp/webhook" -Method POST -ContentType 'application/json' -Body $payload -UseBasicParsing -ErrorAction Stop
  $lines += "WHATSAPP_WEBHOOK_STATUS=$($r.StatusCode)"
  $lines += "WHATSAPP_WEBHOOK_BODY=$($r.Content)"
} catch {
  if ($_.Exception.Response) {
    $lines += "WHATSAPP_WEBHOOK_STATUS=$([int]$_.Exception.Response.StatusCode)"
  } else {
    $lines += 'WHATSAPP_WEBHOOK_STATUS=ERROR'
    $lines += $_.Exception.Message
  }
}

$out = 'c:/ClinicQueueFinal/ClinicQueue2/ClinicQueue/_feature_fix_verification.txt'
$lines | Set-Content $out
Get-Content $out
