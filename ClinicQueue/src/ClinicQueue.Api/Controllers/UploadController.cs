using ClinicQueue.Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQueue.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IDocumentExtractionService _extractionService;
    private readonly ILogger<UploadController> _logger;

    private const int MaxFileSizeBytes = 10 * 1024 * 1024;

    public UploadController(
        IDocumentExtractionService extractionService,
        ILogger<UploadController> logger)
    {
        _extractionService = extractionService;
        _logger = logger;
    }

    [HttpPost("extract-text")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    [ProducesResponseType(typeof(ExtractionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExtractText(IFormFile file)
    {
        _logger.LogInformation("Received file upload request");

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

        var result = await _extractionService.ExtractTextAsync(fileBytes, file.FileName);

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

    [HttpGet("health")]
    [ProducesResponseType(typeof(HealthCheckResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> HealthCheck([FromServices] IOcrService ocrService)
    {
        var ocrHealthy = await ocrService.IsServiceHealthyAsync();

        return Ok(new HealthCheckResponse
        {
            Status = ocrHealthy ? "Healthy" : "Degraded",
            PdfExtractionAvailable = true,
            OcrServiceAvailable = ocrHealthy,
            Message = ocrHealthy
                ? "All services operational"
                : "OCR microservice is not responding - image extraction may fail"
        });
    }
}

public class ExtractionResponse
{
    public bool Success { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string ExtractionMethod { get; set; } = string.Empty;
    public double? Confidence { get; set; }
    public int WordCount { get; set; }
}

public class ErrorResponse
{
    public bool Success { get; set; }
    public string Error { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? FileName { get; set; }
}

public class SupportedFormatsApiResponse
{
    public List<string> SupportedFormats { get; set; } = [];
    public List<string> PdfFormats { get; set; } = [];
    public List<string> ImageFormats { get; set; } = [];
    public int MaxFileSizeBytes { get; set; }
    public int MaxFileSizeMB { get; set; }
}

public class HealthCheckResponse
{
    public string Status { get; set; } = string.Empty;
    public bool PdfExtractionAvailable { get; set; }
    public bool OcrServiceAvailable { get; set; }
    public string Message { get; set; } = string.Empty;
}
