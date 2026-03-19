# 🔍 Image OCR Microservice

A beginner-friendly microservice for extracting text from medical images using PaddleOCR.

---

## 📚 Table of Contents

1. [Overview](#overview)
2. [Architecture Explained (WWH)](#architecture-explained-wwh)
3. [Technology Stack](#technology-stack)
4. [Installation](#installation)
5. [Running the Service](#running-the-service)
6. [API Documentation](#api-documentation)
7. [Integration with .NET Backend](#integration-with-net-backend)
8. [Testing](#testing)
9. [Troubleshooting](#troubleshooting)

---

## Overview

### What Is This?

This is a **microservice** that extracts text from images. When you upload an image (like a prescription, medical report, or scanned document), it reads the text and returns it as structured data.

### Why a Microservice?

A **microservice** is a small, independent application that does ONE thing well. Instead of building OCR into the main .NET backend:

| Approach | Pros | Cons |
|----------|------|------|
| **Monolith** (all in one) | Simple deployment | Hard to scale, Python+C# mixing is complex |
| **Microservice** (separate) | Independent scaling, best tools for each job | More moving parts |

For OCR, Python has the best libraries (PaddleOCR, Tesseract), so we use a Python microservice.

---

## Architecture Explained (WWH)

### WHAT is this architecture?

```
┌─────────────────────────────────────────────────────────────────┐
│                     CLINIC QUEUE SYSTEM                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌─────────────┐       ┌─────────────┐      ┌─────────────┐   │
│  │   Frontend  │──────►│ .NET Backend│─────►│OCR Service  │   │
│  │   (React)   │       │  (Port 5000)│      │ (Port 8001) │   │
│  └─────────────┘       └──────┬──────┘      └─────────────┘   │
│                               │                                 │
│                               ▼                                 │
│                        ┌─────────────┐                         │
│                        │  Database   │                         │
│                        │  (SQLite)   │                         │
│                        └─────────────┘                         │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### WHY this design?

1. **Separation of Concerns**: Each service handles one responsibility
2. **Language Flexibility**: Use Python for OCR (best libraries), C# for business logic
3. **Independent Scaling**: If OCR becomes a bottleneck, scale it independently
4. **Fault Isolation**: If OCR service crashes, the main app keeps working

### HOW does it work?

1. User uploads an image in the frontend
2. Frontend sends file to .NET backend
3. .NET backend detects file type:
   - **Image?** → Send to OCR Microservice (this service!)
   - **PDF?** → Use PDF extraction library
4. OCR service processes image and returns text
5. .NET backend uses the text (stores it, analyzes it, etc.)

---

## Technology Stack

| Technology | What It Is | Why We Use It |
|------------|------------|---------------|
| **Python 3.9+** | Programming language | Best ecosystem for OCR/ML |
| **FastAPI** | Web framework | Fast, modern, auto-docs |
| **PaddleOCR** | OCR engine | Accurate, supports 80+ languages |
| **OpenCV** | Image processing | Preprocessing for better OCR |
| **Uvicorn** | ASGI server | Production-ready server |
| **Pydantic** | Data validation | Type-safe request/response |

---

## Installation

### Prerequisites

- Python 3.9 or higher
- pip (Python package manager)

### Step 1: Navigate to the OCR Service Directory

```powershell
cd C:\ClinicQueueFinal\ClinicQueue2\OCRService
```

### Step 2: Create Virtual Environment (Recommended)

```powershell
# Create virtual environment
python -m venv venv

# Activate it (Windows)
.\venv\Scripts\Activate

# Activate it (Linux/Mac)
source venv/bin/activate
```

### Step 3: Install Dependencies

```powershell
pip install -r requirements.txt
```

⚠️ **Note**: PaddleOCR and PaddlePaddle are large packages (~500MB+). First installation takes time.

### Step 4: Verify Installation

```powershell
python -c "from paddleocr import PaddleOCR; print('PaddleOCR OK')"
```

---

## Running the Service

### Development Mode (with auto-reload)

```powershell
cd app
uvicorn main:app --reload --port 8001
```

### Production Mode

```powershell
cd app
uvicorn main:app --host 0.0.0.0 --port 8001 --workers 4
```

### Expected Output

```
INFO:     Started server process [12345]
INFO:     Waiting for application startup.
INFO:     🚀 Starting OCR Microservice...
INFO:     📥 Loading PaddleOCR model (this may take a few seconds)...
INFO:     ✅ PaddleOCR model loaded successfully!
INFO:     Application startup complete.
INFO:     Uvicorn running on http://127.0.0.1:8001 (Press CTRL+C to quit)
```

### Verify It's Running

Open browser: http://localhost:8001

You should see:
```json
{
    "service": "Image OCR Microservice",
    "status": "running",
    "version": "1.0.0"
}
```

---

## API Documentation

### Interactive Docs

FastAPI automatically generates documentation:

- **Swagger UI**: http://localhost:8001/docs
- **ReDoc**: http://localhost:8001/redoc

### Endpoints

#### 1. Extract Text (Simple)

**POST** `/api/v1/extract-text`

Extracts text from an uploaded image.

**Request:**
- Content-Type: `multipart/form-data`
- Body: `file` - The image file

**Response (Success):**
```json
{
    "success": true,
    "extracted_text": "Patient Name: John Doe\nAge: 45\nMedicine: Paracetamol 500mg",
    "confidence": 0.92,
    "word_count": 8
}
```

**Response (Error):**
```json
{
    "success": false,
    "error": "INVALID_FILE_TYPE",
    "message": "Unsupported file type '.pdf'. Supported formats: .jpg, .jpeg, .png, .bmp, .tiff"
}
```

#### 2. Extract Text (Detailed)

**POST** `/api/v1/extract-text/detailed`

Returns per-line OCR results with confidence scores and positions.

**Response:**
```json
{
    "success": true,
    "extracted_text": "Patient Name: John Doe\nAge: 45",
    "lines": [
        {
            "text": "Patient Name: John Doe",
            "confidence": 0.95,
            "bounding_box": [[10, 20], [200, 20], [200, 50], [10, 50]]
        },
        {
            "text": "Age: 45",
            "confidence": 0.89,
            "bounding_box": [[10, 60], [100, 60], [100, 90], [10, 90]]
        }
    ],
    "average_confidence": 0.92,
    "total_lines": 2
}
```

#### 3. Health Check

**GET** `/health`

```json
{
    "status": "healthy",
    "ocr_model_loaded": true,
    "message": "OCR service is ready"
}
```

#### 4. Supported Formats

**GET** `/api/v1/supported-formats`

```json
{
    "supported_formats": [".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif"],
    "description": "Image files with these extensions can be processed"
}
```

---

## Integration with .NET Backend

### How It Works

The .NET backend should:

1. **Receive file upload** from frontend
2. **Detect file type** by extension or MIME type
3. **Route to appropriate service**:
   - Images → OCR Microservice
   - PDFs → PDF extraction (PdfExtractionService)
4. **Process returned text**

### Example .NET Integration Code

Add this to your .NET backend (e.g., in a service class):

```csharp
// In your .NET backend - Example integration

using System.Net.Http;
using System.Net.Http.Headers;

public class DocumentExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly string _ocrServiceUrl = "http://localhost:8001";

    // Image extensions that should go to OCR service
    private readonly HashSet<string> _imageExtensions = new()
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif"
    };

    public DocumentExtractionService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> ExtractTextFromFile(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLower();

        if (_imageExtensions.Contains(extension))
        {
            // Send to OCR microservice
            return await ExtractTextFromImage(file);
        }
        else if (extension == ".pdf")
        {
            // Use PDF extraction service
            return await ExtractTextFromPdf(file);
        }
        else
        {
            throw new NotSupportedException($"File type {extension} is not supported");
        }
    }

    private async Task<string> ExtractTextFromImage(IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);
        
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", file.FileName);

        var response = await _httpClient.PostAsync(
            $"{_ocrServiceUrl}/api/v1/extract-text", 
            content
        );

        var result = await response.Content.ReadAsStringAsync();
        var ocrResponse = JsonSerializer.Deserialize<OcrResponse>(result);

        if (ocrResponse?.Success == true)
        {
            return ocrResponse.ExtractedText;
        }

        throw new Exception($"OCR failed: {ocrResponse?.Message}");
    }

    private async Task<string> ExtractTextFromPdf(IFormFile file)
    {
        // Your existing PDF extraction logic
        // e.g., using PdfExtractionService
        throw new NotImplementedException();
    }
}

// Response model
public class OcrResponse
{
    public bool Success { get; set; }
    public string ExtractedText { get; set; }
    public string Error { get; set; }
    public string Message { get; set; }
    public double? Confidence { get; set; }
}
```

### Architecture Flow

```
User uploads file
       │
       ▼
┌──────────────────┐
│  .NET Backend    │
│                  │
│  1. Receive file │
│  2. Check type   │
└────────┬─────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌──────┐  ┌──────┐
│Image │  │ PDF  │
└──┬───┘  └──┬───┘
   │         │
   ▼         ▼
┌──────────────────┐  ┌──────────────────┐
│ OCR Microservice │  │ PDF Extraction   │
│    (Python)      │  │    (.NET)        │
│   Port 8001      │  │                  │
└────────┬─────────┘  └────────┬─────────┘
         │                     │
         └──────────┬──────────┘
                    │
                    ▼
            Extracted Text
```

---

## Testing

### Using cURL

```bash
# Basic text extraction
curl -X POST "http://localhost:8001/api/v1/extract-text" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@/path/to/your/image.jpg"

# With preprocessing disabled
curl -X POST "http://localhost:8001/api/v1/extract-text?preprocess=false" \
  -F "file=@/path/to/your/image.jpg"

# Health check
curl http://localhost:8001/health
```

### Using PowerShell

```powershell
# Upload and extract text
$filePath = "C:\path\to\your\image.jpg"
$uri = "http://localhost:8001/api/v1/extract-text"

$fileBytes = [System.IO.File]::ReadAllBytes($filePath)
$fileEnc = [System.Text.Encoding]::GetEncoding('ISO-8859-1').GetString($fileBytes)
$boundary = [System.Guid]::NewGuid().ToString()

$LF = "`r`n"
$bodyLines = (
    "--$boundary",
    "Content-Disposition: form-data; name=`"file`"; filename=`"image.jpg`"",
    "Content-Type: image/jpeg$LF",
    $fileEnc,
    "--$boundary--$LF"
) -join $LF

Invoke-RestMethod -Uri $uri -Method Post -ContentType "multipart/form-data; boundary=`"$boundary`"" -Body $bodyLines
```

### Using Postman

1. Open Postman
2. Create new request → **POST**
3. URL: `http://localhost:8001/api/v1/extract-text`
4. Body tab → Select **form-data**
5. Add key: `file` → Type: **File** → Select your image
6. Click **Send**

### Using Python

```python
import requests

url = "http://localhost:8001/api/v1/extract-text"
files = {"file": open("test_image.jpg", "rb")}

response = requests.post(url, files=files)
print(response.json())
```

---

## Troubleshooting

### Problem: "ModuleNotFoundError: No module named 'paddleocr'"

**Solution:** Install PaddleOCR
```powershell
pip install paddleocr paddlepaddle
```

### Problem: "PaddlePaddle is not installed"

**Solution:** PaddlePaddle might need specific version
```powershell
pip install paddlepaddle==2.5.2
```

### Problem: Service starts but OCR fails

**Check:**
1. Is the model downloaded? First run downloads ~500MB of models
2. Is there enough memory? PaddleOCR needs ~2GB RAM
3. Check logs for specific errors

### Problem: "Connection refused" from .NET backend

**Solution:** Ensure OCR service is running on port 8001
```powershell
# Check if port is in use
netstat -ano | findstr :8001
```

### Problem: Slow first request

**Reason:** On first request, PaddleOCR may download model files (~500MB)

**Solution:** Pre-download models by running the service once before production use

### Problem: Low OCR accuracy

**Try:**
1. Ensure image is high quality (300 DPI minimum)
2. Check if preprocessing helps: try `preprocess=true`
3. For skewed images, edit `preprocess_image()` to enable `deskew=True`

---

## Project Structure

```
OCRService/
├── requirements.txt          # Python dependencies
├── README.md                 # This file
└── app/
    ├── main.py               # Application entry point
    ├── __init__.py
    ├── routes/
    │   ├── __init__.py
    │   └── ocr_routes.py     # API endpoints
    ├── services/
    │   ├── __init__.py
    │   └── ocr_service.py    # OCR business logic
    ├── utils/
    │   ├── __init__.py
    │   └── image_preprocessing.py  # OpenCV utilities
    └── models/
        ├── __init__.py
        └── response_model.py  # Pydantic models
```

---

## Key Concepts Summary (WWH)

### FastAPI
- **WHAT**: Modern Python web framework
- **WHY**: Fast, automatic documentation, type validation
- **HOW**: Define routes with decorators, use Pydantic for validation

### PaddleOCR
- **WHAT**: Open-source OCR engine by Baidu
- **WHY**: Accurate, supports many languages, works offline
- **HOW**: Load model once, call `ocr()` method on images

### Microservice Pattern
- **WHAT**: Small, independent service doing one thing
- **WHY**: Scalability, technology flexibility, fault isolation
- **HOW**: Communicate via HTTP/REST, run on separate port

### Image Preprocessing
- **WHAT**: Transformations to improve OCR accuracy
- **WHY**: Raw images often have noise, poor contrast
- **HOW**: Grayscale, denoising, contrast enhancement, thresholding

---

## License

This project is part of the ClinicQueue system.

---

## Support

For issues or questions:
1. Check the [Troubleshooting](#troubleshooting) section
2. Review API docs at http://localhost:8001/docs
3. Check service logs for error messages
