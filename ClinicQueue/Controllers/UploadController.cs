/*
=============================================================================
UploadController.cs - FILE UPLOAD API ENDPOINT
=============================================================================

WWH EXPLANATION:

WHAT:
    An ASP.NET Core API Controller that:
    - Accepts file uploads via HTTP POST
    - Validates uploaded files
    - Delegates text extraction to DocumentExtractionService
    - Returns extracted text in JSON format

WHY:
    * API ENTRY POINT: This is where HTTP requests come in from clients
      (frontend, Postman, other services).
    
    * VALIDATION: Controllers handle input validation before calling services.
    
    * RESPONSE FORMATTING: Converts service results to HTTP responses with
      appropriate status codes.
    
    * THIN CONTROLLER: Business logic is in services, controller just coordinates.

HOW HTTP FILE UPLOAD WORKS:

    ┌─────────────────────────────────────────────────────────────────────┐
    │                        HTTP Request                                 │
    ├─────────────────────────────────────────────────────────────────────┤
    │ POST /api/upload/extract-text                                       │
    │ Content-Type: multipart/form-data                                   │
    │                                                                     │
    │ ┌─────────────────────────────────────────────────────────────────┐ │
    │ │ --boundary                                                      │ │
    │ │ Content-Disposition: form-data; name="file"; filename="doc.pdf" │ │
    │ │ Content-Type: application/pdf                                   │ │
    │ │                                                                 │ │
    │ │ [binary file data]                                              │ │
    │ │ --boundary--                                                    │ │
    │ └─────────────────────────────────────────────────────────────────┘ │
    └─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
    ┌─────────────────────────────────────────────────────────────────────┐
    │                      ASP.NET Core                                   │
    │                                                                     │
    │  IFormFile file = parsed from multipart data                        │
    │  file.FileName = "doc.pdf"                                          │
    │  file.Length = size in bytes                                        │
    │  file.OpenReadStream() = to read content                            │
    └─────────────────────────────────────────────────────────────────────┘

=============================================================================
*/

using Microsoft.AspNetCore.Mvc;
using ClinicQueue.Services;

namespace ClinicQueue.Controllers
{
    /// <summary>
    /// API Controller for file upload and text extraction.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        // ─── Dependencies ────────────────────────────────────────────────────────

        private readonly IDocumentExtractionService _extractionService;
        private readonly ILogger<UploadController> _logger;

        // ─── Configuration ───────────────────────────────────────────────────────

        private const int MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

        /*
        =============================================================================
        CONSTRUCTOR
        =============================================================================
        
        WWH:
        WHAT: Receives IDocumentExtractionService from DI
        WHY: Controller delegates work to service, doesn't do extraction itself
        HOW: ASP.NET Core DI automatically provides the service
        
        =============================================================================
        */

        public UploadController(
            IDocumentExtractionService extractionService,
            ILogger<UploadController> logger)
        {
            _extractionService = extractionService;
            _logger = logger;
        }

        /*
        =============================================================================
        EXTRACT TEXT ENDPOINT
        =============================================================================
        
        HTTP: POST /api/upload/extract-text
        
        WWH:
        WHAT: Main endpoint for uploading documents and getting extracted text
        
        WHY: Provides a REST API that frontend and other services can call
        
        HOW:
            1. Receive IFormFile (ASP.NET parses multipart/form-data)
            2. Validate file (not null, not empty, size limit)
            3. Read file bytes
            4. Call extraction service
            5. Return JSON response
        
        EXAMPLE CURL:
            curl -X POST http://localhost:5000/api/upload/extract-text \
                 -F "file=@document.pdf"
        
        =============================================================================
        */

        [HttpPost("extract-text")]
        [RequestSizeLimit(MaxFileSizeBytes)]
        [ProducesResponseType(typeof(ExtractionResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> ExtractText(IFormFile file)
        {
            _logger.LogInformation("Received file upload request");

            // ─── STEP 1: Validate file is provided ───────────────────────────────
            /*
            WWH:
            WHAT: Check if a file was actually uploaded
            WHY: IFormFile can be null if no file in request
            HOW: Simple null/length check
            */

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("No file uploaded or file is empty");
                return BadRequest(new ErrorResponse
                {
                    Success = false,
                    Error = "NO_FILE",
                    Message = "Please upload a file",
                    Details = "The request must include a file in the 'file' form field"
                });
            }

            _logger.LogInformation(
                "Processing file: {FileName}, Size: {Size} bytes, ContentType: {ContentType}",
                file.FileName, file.Length, file.ContentType);

            // ─── STEP 2: Validate file size ──────────────────────────────────────

            if (file.Length > MaxFileSizeBytes)
            {
                _logger.LogWarning("File too large: {Size} bytes", file.Length);
                return BadRequest(new ErrorResponse
                {
                    Success = false,
                    Error = "FILE_TOO_LARGE",
                    Message = $"File exceeds maximum size of {MaxFileSizeBytes / 1024 / 1024} MB",
                    Details = $"Uploaded file size: {file.Length / 1024} KB"
                });
            }

            // ─── STEP 3: Validate file type is supported ─────────────────────────

            if (!_extractionService.IsSupported(file.FileName))
            {
                var extension = Path.GetExtension(file.FileName);
                var supported = string.Join(", ", _extractionService.GetSupportedExtensions());

                _logger.LogWarning("Unsupported file type: {Extension}", extension);
                return BadRequest(new ErrorResponse
                {
                    Success = false,
                    Error = "UNSUPPORTED_FORMAT",
                    Message = $"File type '{extension}' is not supported",
                    Details = $"Supported formats: {supported}"
                });
            }

            // ─── STEP 4: Read file bytes ─────────────────────────────────────────
            /*
            WWH:
            WHAT: Convert the uploaded file stream to a byte array
            WHY: Our services expect byte[] for processing
            HOW: Use MemoryStream and CopyToAsync
            */

            byte[] fileBytes;
            try
            {
                using var memoryStream = new MemoryStream();
                await file.CopyToAsync(memoryStream);
                fileBytes = memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read uploaded file");
                return StatusCode(500, new ErrorResponse
                {
                    Success = false,
                    Error = "READ_ERROR",
                    Message = "Failed to read the uploaded file",
                    Details = ex.Message
                });
            }

            // ─── STEP 5: Extract text using service ──────────────────────────────
            /*
            WWH:
            WHAT: Delegate to DocumentExtractionService for actual extraction
            WHY: Controller shouldn't contain extraction logic
            HOW: Pass bytes and filename, get unified result back
            */

            var result = await _extractionService.ExtractTextAsync(fileBytes, file.FileName);

            // ─── STEP 6: Return appropriate response ─────────────────────────────

            if (result.Success)
            {
                return Ok(new ExtractionResponse
                {
                    Success = true,
                    ExtractedText = result.ExtractedText,
                    FileName = file.FileName,
                    FileType = result.FileType.ToString(),
                    ExtractionMethod = result.ExtractionMethod,
                    Confidence = result.Confidence,
                    WordCount = result.WordCount
                });
            }
            else
            {
                // Extraction failed - return error
                return StatusCode(
                    result.ErrorCode == "SERVICE_UNAVAILABLE" ? 503 : 500,
                    new ErrorResponse
                    {
                        Success = false,
                        Error = result.ErrorCode ?? "EXTRACTION_FAILED",
                        Message = result.ErrorMessage ?? "Text extraction failed",
                        FileName = file.FileName
                    });
            }
        }

        /*
        =============================================================================
        SUPPORTED FORMATS ENDPOINT
        =============================================================================
        
        HTTP: GET /api/upload/supported-formats
        
        =============================================================================
        */

        [HttpGet("supported-formats")]
        [ProducesResponseType(typeof(SupportedFormatsApiResponse), StatusCodes.Status200OK)]
        public IActionResult GetSupportedFormats()
        {
            var formats = _extractionService.GetSupportedExtensions();

            return Ok(new SupportedFormatsApiResponse
            {
                SupportedFormats = formats,
                PdfFormats = formats.Where(f => f.Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToList(),
                ImageFormats = formats.Where(f => !f.Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToList(),
                MaxFileSizeBytes = MaxFileSizeBytes,
                MaxFileSizeMB = MaxFileSizeBytes / 1024 / 1024
            });
        }

        /*
        =============================================================================
        HEALTH CHECK ENDPOINT
        =============================================================================
        
        HTTP: GET /api/upload/health
        
        Checks if the OCR microservice is reachable.
        
        =============================================================================
        */

        [HttpGet("health")]
        [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
        public async Task<IActionResult> HealthCheck([FromServices] IOcrService ocrService)
        {
            var ocrHealthy = await ocrService.IsServiceHealthyAsync();

            return Ok(new HealthCheckResponse
            {
                Status = ocrHealthy ? "Healthy" : "Degraded",
                PdfExtractionAvailable = true,  // PdfPig is always available (local)
                OcrServiceAvailable = ocrHealthy,
                Message = ocrHealthy
                    ? "All services operational"
                    : "OCR microservice is not responding - image extraction may fail"
            });
        }
    }

    /*
    =============================================================================
    RESPONSE MODELS
    =============================================================================
    
    WWH:
    WHAT: DTOs (Data Transfer Objects) that define the shape of API responses
    WHY: Ensures consistent response format, enables Swagger documentation
    HOW: Simple classes with properties, serialized to JSON by ASP.NET
    
    =============================================================================
    */

    /// <summary>
    /// Successful text extraction response.
    /// </summary>
    public class ExtractionResponse
    {
        /// <summary>Always true for success responses</summary>
        public bool Success { get; set; }

        /// <summary>The extracted text content</summary>
        public string ExtractedText { get; set; } = string.Empty;

        /// <summary>Original uploaded filename</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>Detected file type (Pdf, Image)</summary>
        public string FileType { get; set; } = string.Empty;

        /// <summary>Method used (PDF (PdfPig) or OCR (PaddleOCR))</summary>
        public string ExtractionMethod { get; set; } = string.Empty;

        /// <summary>OCR confidence score (null for PDF)</summary>
        public double? Confidence { get; set; }

        /// <summary>Number of words extracted</summary>
        public int WordCount { get; set; }
    }

    /// <summary>
    /// Error response for failed operations.
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>Always false for error responses</summary>
        public bool Success { get; set; }

        /// <summary>Error code (e.g., NO_FILE, UNSUPPORTED_FORMAT)</summary>
        public string Error { get; set; } = string.Empty;

        /// <summary>Human-readable error message</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Additional error details</summary>
        public string? Details { get; set; }

        /// <summary>Filename if applicable</summary>
        public string? FileName { get; set; }
    }

    /// <summary>
    /// Response for supported formats endpoint.
    /// </summary>
    public class SupportedFormatsApiResponse
    {
        public List<string> SupportedFormats { get; set; } = new();
        public List<string> PdfFormats { get; set; } = new();
        public List<string> ImageFormats { get; set; } = new();
        public int MaxFileSizeBytes { get; set; }
        public int MaxFileSizeMB { get; set; }
    }

    /// <summary>
    /// Response for health check endpoint.
    /// </summary>
    public class HealthCheckResponse
    {
        public string Status { get; set; } = string.Empty;
        public bool PdfExtractionAvailable { get; set; }
        public bool OcrServiceAvailable { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
