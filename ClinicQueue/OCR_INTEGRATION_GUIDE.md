# 🔗 .NET Backend + OCR Microservice Integration Guide

A beginner-friendly guide explaining how the .NET backend integrates with the Python OCR microservice.

---

## 📚 Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Complete Processing Flow](#complete-processing-flow)
3. [Code Structure Explained (WWH)](#code-structure-explained-wwh)
4. [How to Run Everything](#how-to-run-everything)
5. [Testing with Postman](#testing-with-postman)
6. [Testing with cURL](#testing-with-curl)
7. [Troubleshooting](#troubleshooting)

---

## Architecture Overview

### System Diagram

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                           CLINIC QUEUE SYSTEM                                │
├──────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌─────────────┐                                                             │
│  │   Client    │  (Postman, Frontend, WhatsApp)                              │
│  └──────┬──────┘                                                             │
│         │                                                                    │
│         │  POST /api/upload/extract-text                                     │
│         │  Content-Type: multipart/form-data                                 │
│         │  Body: file=@document.pdf or file=@image.jpg                       │
│         │                                                                    │
│         ▼                                                                    │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │                    .NET BACKEND (Port 5000)                           │   │
│  │                                                                       │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │ UploadController                                                 │ │   │
│  │  │   └─> ExtractText(IFormFile file)                               │ │   │
│  │  │         │                                                        │ │   │
│  │  │         ▼ Validate file                                          │ │   │
│  │  │         │                                                        │ │   │
│  │  │         ▼ Call IDocumentExtractionService                        │ │   │
│  │  └─────────┼────────────────────────────────────────────────────────┘ │   │
│  │            │                                                          │   │
│  │            ▼                                                          │   │
│  │  ┌─────────────────────────────────────────────────────────────────┐ │   │
│  │  │ DocumentExtractionService                                        │ │   │
│  │  │   │                                                              │ │   │
│  │  │   ├─> DetectFileType(extension)                                  │ │   │
│  │  │   │        │                                                     │ │   │
│  │  │   │   ┌────┴────┐                                                │ │   │
│  │  │   │   │         │                                                │ │   │
│  │  │   │   ▼         ▼                                                │ │   │
│  │  │   │  PDF       Image                                             │ │   │
│  │  │   │   │         │                                                │ │   │
│  │  │   │   │         │                                                │ │   │
│  │  │   ▼   │         │                                                │ │   │
│  │  └───────┼─────────┼────────────────────────────────────────────────┘ │   │
│  │          │         │                                                  │   │
│  │          │         └────────────────────────────────────────┐         │   │
│  │          ▼                                                  ▼         │   │
│  │  ┌─────────────────┐                              ┌──────────────┐   │   │
│  │  │ PdfExtraction   │                              │  OcrService  │   │   │
│  │  │ Service         │                              │              │   │   │
│  │  │ (UglyToad.      │                              │ HTTP POST    │   │   │
│  │  │  PdfPig)        │                              │ multipart/   │   │   │
│  │  │                 │                              │ form-data    │   │   │
│  │  │ LOCAL PROCESS   │                              └──────┬───────┘   │   │
│  │  └────────┬────────┘                                     │           │   │
│  │           │                                              │           │   │
│  └───────────┼──────────────────────────────────────────────┼───────────┘   │
│              │                                              │               │
│              │                                              │ HTTP          │
│              │                                              │               │
│              │                                              ▼               │
│              │                              ┌───────────────────────────┐   │
│              │                              │  OCR MICROSERVICE         │   │
│              │                              │  (Python/FastAPI)         │   │
│              │                              │  Port 8001                │   │
│              │                              │                           │   │
│              │                              │  ┌───────────────────┐    │   │
│              │                              │  │ PaddleOCR Engine  │    │   │
│              │                              │  │                   │    │   │
│              │                              │  │ 1. Preprocess     │    │   │
│              │                              │  │ 2. Detect text    │    │   │
│              │                              │  │ 3. Recognize text │    │   │
│              │                              │  └─────────┬─────────┘    │   │
│              │                              │            │              │   │
│              │                              └────────────┼──────────────┘   │
│              │                                           │                  │
│              │                                           │ JSON Response    │
│              │                                           │                  │
│              ▼                                           ▼                  │
│  ┌──────────────────────────────────────────────────────────────────────┐  │
│  │                    DocumentExtractionResult                           │  │
│  │                                                                       │  │
│  │    {                                                                  │  │
│  │      "success": true,                                                 │  │
│  │      "extractedText": "...",                                          │  │
│  │      "fileType": "Pdf" | "Image",                                     │  │
│  │      "extractionMethod": "PDF (PdfPig)" | "OCR (PaddleOCR)",          │  │
│  │      "confidence": 0.95,  // Only for images                          │  │
│  │      "wordCount": 42                                                  │  │
│  │    }                                                                  │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Complete Processing Flow

### Step-by-Step Flow

```
1. USER UPLOADS FILE
   └─> HTTP POST to /api/upload/extract-text
   └─> File attached as multipart/form-data

2. UPLOAD CONTROLLER receives request
   └─> Validates file exists
   └─> Validates file size (max 10MB)
   └─> Validates file extension is supported
   └─> Reads file into byte[]

3. DOCUMENT EXTRACTION SERVICE analyzes file
   └─> Gets file extension (.pdf, .jpg, etc.)
   └─> Determines FileTypeCategory (Pdf, Image, Unsupported)

4A. IF PDF:
    └─> Calls IPdfExtractionService.ExtractTextFromPdf(bytes)
    └─> PdfPig opens PDF, reads all pages
    └─> Returns extracted text

4B. IF IMAGE:
    └─> Calls IOcrService.ExtractTextFromImageAsync(bytes, filename)
    └─> OcrService creates MultipartFormDataContent
    └─> Sends HTTP POST to http://localhost:8001/api/v1/extract-text
    └─> OCR Microservice processes image with PaddleOCR
    └─> Returns JSON: { success, extracted_text, confidence }
    └─> OcrService parses JSON into OcrResult

5. RESULT RETURNED
   └─> DocumentExtractionService wraps result in DocumentExtractionResult
   └─> Controller returns JSON to client
```

---

## Code Structure Explained (WWH)

### Files Created

```
ClinicQueue/
├── Controllers/
│   └── UploadController.cs       # API endpoint for file uploads
├── Services/
│   ├── IOcrService.cs            # Interface for OCR microservice calls
│   ├── OcrService.cs             # Implementation - HTTP calls to Python
│   ├── IDocumentExtractionService.cs  # Interface for unified extraction
│   └── DocumentExtractionService.cs   # Routes PDF vs Image
└── appsettings.json              # Added OcrServiceUrl configuration
```

---

### 1. IOcrService.cs - Interface

**WHAT:** Defines the contract for OCR operations.

**WHY:** 
- Enables dependency injection
- Makes testing easy (can mock the interface)
- Decouples controller from HTTP implementation

**HOW:** Declares methods like `ExtractTextFromImageAsync(byte[] imageBytes, string fileName)`

```csharp
public interface IOcrService
{
    Task<OcrResult> ExtractTextFromImageAsync(byte[] imageBytes, string fileName);
    Task<bool> IsServiceHealthyAsync();
}
```

---

### 2. OcrService.cs - HTTP Client

**WHAT:** Makes HTTP calls to the Python OCR microservice.

**WHY:**
- .NET can't run PaddleOCR directly (it's Python)
- Encapsulates all HTTP/network logic in one place
- Handles errors, timeouts, response parsing

**HOW:**

```csharp
// 1. Create MultipartFormDataContent (like a web form with file)
using var formContent = new MultipartFormDataContent();
var fileContent = new ByteArrayContent(imageBytes);
fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
formContent.Add(fileContent, "file", fileName);

// 2. Send POST request to OCR microservice
var response = await _httpClient.PostAsync(
    "http://localhost:8001/api/v1/extract-text",
    formContent
);

// 3. Parse JSON response
var json = await response.Content.ReadAsStringAsync();
var result = JsonSerializer.Deserialize<OcrApiResponse>(json);
```

---

### 3. DocumentExtractionService.cs - Router

**WHAT:** Routes files to the correct extraction service based on type.

**WHY:**
- Controllers don't need to know about PDF vs OCR logic
- Single point of entry for all document extraction
- Easy to add new file types (e.g., Word documents)

**HOW:**

```csharp
public async Task<DocumentExtractionResult> ExtractTextAsync(byte[] fileBytes, string fileName)
{
    var fileType = DetectFileType(fileName);  // Check extension
    
    return fileType switch
    {
        FileTypeCategory.Pdf => await ExtractFromPdfAsync(fileBytes, fileName),
        FileTypeCategory.Image => await ExtractFromImageAsync(fileBytes, fileName),
        _ => DocumentExtractionResult.UnsupportedFormat(extension)
    };
}
```

---

### 4. UploadController.cs - API Endpoint

**WHAT:** HTTP endpoint that receives file uploads.

**WHY:**
- Entry point for external clients (frontend, Postman)
- Handles HTTP-specific concerns (status codes, validation)
- Delegates business logic to services

**HOW:**

```csharp
[HttpPost("extract-text")]
public async Task<IActionResult> ExtractText(IFormFile file)
{
    // Validate
    if (file == null) return BadRequest("No file uploaded");
    
    // Read bytes
    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    var bytes = ms.ToArray();
    
    // Extract
    var result = await _extractionService.ExtractTextAsync(bytes, file.FileName);
    
    // Return JSON
    return Ok(result);
}
```

---

### 5. Configuration in appsettings.json

**WHAT:** Stores the OCR service URL.

**WHY:**
- Don't hardcode URLs in code
- Easy to change for different environments
- Can use environment variables in production

```json
{
  "AI": {
    "OcrServiceUrl": "http://localhost:8001",
    "TranslationServiceUrl": "http://localhost:5001"
  }
}
```

---

### 6. Service Registration in Program.cs

**WHAT:** Registers services with the DI container.

**WHY:**
- ASP.NET Core needs to know how to create services
- AddHttpClient configures HTTP client lifecycle
- Services can then be injected into controllers

```csharp
// OCR Service - typed HttpClient
builder.Services.AddHttpClient<IOcrService, OcrService>(client =>
{
    client.BaseAddress = new Uri("http://localhost:8001");
    client.Timeout = TimeSpan.FromSeconds(60);
});

// Document Extraction - combines PDF and OCR
builder.Services.AddScoped<IDocumentExtractionService, DocumentExtractionService>();
```

---

## How to Run Everything

### Prerequisites

- .NET 8 SDK installed
- Python 3.9+ installed
- OCR microservice dependencies installed

### Step 1: Start OCR Microservice

```powershell
# Terminal 1 - OCR Service
cd C:\ClinicQueueFinal\ClinicQueue2\OCRService\app
pip install -r ../requirements.txt   # First time only
uvicorn main:app --reload --port 8001
```

Expected output:
```
INFO:     Loading PaddleOCR model...
INFO:     PaddleOCR model loaded successfully!
INFO:     Uvicorn running on http://127.0.0.1:8001
```

### Step 2: Start .NET Backend

```powershell
# Terminal 2 - .NET Backend
cd C:\ClinicQueueFinal\ClinicQueue2\ClinicQueue
dotnet run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### Step 3: Verify Both Services

```powershell
# Check OCR service
curl http://localhost:8001/health

# Check .NET upload service
curl http://localhost:5000/api/upload/health
```

---

## Testing with Postman

### Test 1: Upload a PDF

1. **Create new request**
2. **Method:** POST
3. **URL:** `http://localhost:5000/api/upload/extract-text`
4. **Body tab:**
   - Select `form-data`
   - Key: `file` (change type to "File" using dropdown)
   - Value: Select a PDF file
5. **Click Send**

**Expected Response:**
```json
{
    "success": true,
    "extractedText": "Patient Report\nName: John Doe\nDate: 2024-01-15\n...",
    "fileName": "report.pdf",
    "fileType": "Pdf",
    "extractionMethod": "PDF (PdfPig)",
    "confidence": null,
    "wordCount": 156
}
```

### Test 2: Upload an Image

1. **Same setup as above**
2. **Select an image file** (jpg, png, etc.)
3. **Click Send**

**Expected Response:**
```json
{
    "success": true,
    "extractedText": "Patient Name: John Doe\nAge: 45\nMedicine: Paracetamol",
    "fileName": "prescription.jpg",
    "fileType": "Image",
    "extractionMethod": "OCR (PaddleOCR)",
    "confidence": 0.92,
    "wordCount": 8
}
```

### Test 3: Unsupported File Type

1. **Upload a .docx or .exe file**
2. **Click Send**

**Expected Response (400 Bad Request):**
```json
{
    "success": false,
    "error": "UNSUPPORTED_FORMAT",
    "message": "File type '.docx' is not supported",
    "details": "Supported formats: .pdf, .jpg, .jpeg, .png, .bmp, .tiff, .tif"
}
```

### Test 4: Health Check

1. **Method:** GET
2. **URL:** `http://localhost:5000/api/upload/health`

**Expected Response:**
```json
{
    "status": "Healthy",
    "pdfExtractionAvailable": true,
    "ocrServiceAvailable": true,
    "message": "All services operational"
}
```

---

## Testing with cURL

### Upload PDF

```bash
curl -X POST http://localhost:5000/api/upload/extract-text \
  -F "file=@C:/path/to/document.pdf"
```

### Upload Image

```bash
curl -X POST http://localhost:5000/api/upload/extract-text \
  -F "file=@C:/path/to/prescription.jpg"
```

### PowerShell Alternative

```powershell
# Upload file using PowerShell
$filePath = "C:\path\to\document.pdf"
$uri = "http://localhost:5000/api/upload/extract-text"

$form = @{
    file = Get-Item -Path $filePath
}

Invoke-RestMethod -Uri $uri -Method Post -Form $form
```

---

## Troubleshooting

### Problem: "Could not connect to OCR service"

**Cause:** OCR microservice is not running

**Solution:**
```powershell
# Check if OCR service is running
curl http://localhost:8001/health

# If not, start it
cd C:\ClinicQueueFinal\ClinicQueue2\OCRService\app
uvicorn main:app --port 8001
```

### Problem: "OCR service request timed out"

**Cause:** Large image or slow machine

**Solution:** Increase timeout in appsettings or OcrService.cs

### Problem: "No text extracted from PDF"

**Cause:** PDF might be image-based (scanned), not text-based

**Solution:** Image-based PDFs need OCR. Consider converting to image first.

### Problem: 503 Service Unavailable

**Cause:** OCR microservice returned error

**Solution:** Check OCR service logs for details

---

## API Reference

### POST /api/upload/extract-text

Extracts text from uploaded PDF or image file.

**Request:**
- Content-Type: `multipart/form-data`
- Body: `file` - The document to process

**Response (Success - 200):**
```json
{
    "success": true,
    "extractedText": "...",
    "fileName": "document.pdf",
    "fileType": "Pdf",
    "extractionMethod": "PDF (PdfPig)",
    "confidence": null,
    "wordCount": 100
}
```

**Response (Error - 400):**
```json
{
    "success": false,
    "error": "UNSUPPORTED_FORMAT",
    "message": "File type '.docx' is not supported"
}
```

### GET /api/upload/supported-formats

Returns list of supported file formats.

### GET /api/upload/health

Returns health status of extraction services.

---

## Summary

| Component | Location | Purpose |
|-----------|----------|---------|
| UploadController | Controllers/ | HTTP endpoint for uploads |
| IOcrService | Services/ | Interface for OCR calls |
| OcrService | Services/ | HTTP client to Python service |
| IDocumentExtractionService | Services/ | Unified extraction interface |
| DocumentExtractionService | Services/ | Routes PDF vs Image |
| IPdfExtractionService | Services/ | PDF extraction interface |
| PdfExtractionService | Services/ | Uses PdfPig library |

---

## Next Steps

1. ✅ Start both services (OCR on 8001, .NET on 5000)
2. ✅ Test with Postman using sample files
3. ✅ Check health endpoints
4. 📋 Integrate with your frontend for file uploads
5. 📋 Add error handling for edge cases
