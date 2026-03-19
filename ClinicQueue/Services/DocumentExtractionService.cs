/*
=============================================================================
DocumentExtractionService.cs - UNIFIED DOCUMENT TEXT EXTRACTION IMPLEMENTATION
=============================================================================

WWH EXPLANATION:

WHAT:
    The implementation of IDocumentExtractionService that:
    - Detects file type from extension
    - Routes PDFs to local PdfPig extraction
    - Routes images to the OCR microservice
    - Returns a unified result format

WHY:
    * STRATEGY PATTERN: Different algorithms (PDF vs OCR) for text extraction,
      selected at runtime based on input.
    
    * DEPENDENCY INJECTION: Uses both IPdfExtractionService and IOcrService,
      injected by the DI container.
    
    * SINGLE POINT OF ENTRY: Controllers have one service to call regardless
      of document type.

HOW:
    1. Controller calls ExtractTextAsync(bytes, filename)
    2. Service checks file extension
    3. If PDF → calls _pdfService.ExtractTextFromPdf()
    4. If Image → calls _ocrService.ExtractTextFromImageAsync()
    5. Result is wrapped in DocumentExtractionResult

=============================================================================
*/

using Microsoft.Extensions.Logging;

namespace ClinicQueue.Services
{
    /// <summary>
    /// Unified document text extraction service.
    /// Routes PDF files to local extraction, images to OCR microservice.
    /// </summary>
    public class DocumentExtractionService : IDocumentExtractionService
    {
        // ─── Dependencies ────────────────────────────────────────────────────────

        private readonly IPdfExtractionService _pdfService;
        private readonly IOcrService _ocrService;
        private readonly ILogger<DocumentExtractionService> _logger;

        // ─── File Extension Sets ─────────────────────────────────────────────────

        private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf"
        };

        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif"
        };

        /*
        =============================================================================
        CONSTRUCTOR
        =============================================================================
        
        WWH:
        WHAT: Receives dependencies from DI container
        WHY: Service needs both PDF and OCR services to do its job
        HOW: .NET DI automatically provides these when creating the service
        
        =============================================================================
        */

        public DocumentExtractionService(
            IPdfExtractionService pdfService,
            IOcrService ocrService,
            ILogger<DocumentExtractionService> logger)
        {
            _pdfService = pdfService;
            _ocrService = ocrService;
            _logger = logger;
        }

        /*
        =============================================================================
        MAIN EXTRACTION METHOD
        =============================================================================
        
        WWH:
        WHAT: The main entry point for text extraction from any supported file
        
        WHY: Callers don't need to know how to handle different file types
        
        HOW:
            1. Detect file type from extension
            2. Route to appropriate extraction service
            3. Return unified result
        
        =============================================================================
        */

        public async Task<DocumentExtractionResult> ExtractTextAsync(byte[] fileBytes, string fileName)
        {
            _logger.LogInformation(
                "Processing document for text extraction: {FileName} ({Size} bytes)",
                fileName, fileBytes.Length);

            // ─── STEP 1: Validate input ──────────────────────────────────────────

            if (fileBytes == null || fileBytes.Length == 0)
            {
                _logger.LogWarning("Empty file received: {FileName}", fileName);
                return DocumentExtractionResult.Failure(
                    "File is empty or missing",
                    "EMPTY_FILE"
                );
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                _logger.LogWarning("No filename provided");
                return DocumentExtractionResult.Failure(
                    "Filename is required",
                    "MISSING_FILENAME"
                );
            }

            // ─── STEP 2: Detect file type ────────────────────────────────────────

            var fileType = DetectFileType(fileName);
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            _logger.LogDebug("Detected file type: {FileType} for extension: {Extension}",
                fileType, extension);

            // ─── STEP 3: Route to appropriate service ────────────────────────────

            return fileType switch
            {
                FileTypeCategory.Pdf => await ExtractFromPdfAsync(fileBytes, fileName),
                FileTypeCategory.Image => await ExtractFromImageAsync(fileBytes, fileName),
                _ => DocumentExtractionResult.UnsupportedFormat(extension)
            };
        }

        /*
        =============================================================================
        PDF EXTRACTION (LOCAL)
        =============================================================================
        
        WWH:
        WHAT: Extracts text from PDF using the local PdfPig library
        WHY: PDFs are processed locally - no need to call external service
        HOW: Calls _pdfService.ExtractTextFromPdf() which uses UglyToad.PdfPig
        
        =============================================================================
        */

        private async Task<DocumentExtractionResult> ExtractFromPdfAsync(byte[] fileBytes, string fileName)
        {
            _logger.LogInformation("Extracting text from PDF: {FileName}", fileName);

            try
            {
                // PdfExtractionService is synchronous, wrap in Task.Run for consistency
                var extractedText = await Task.Run(() => _pdfService.ExtractTextFromPdf(fileBytes));

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    _logger.LogWarning("No text extracted from PDF: {FileName}", fileName);
                    return new DocumentExtractionResult
                    {
                        Success = true,  // Extraction succeeded, just no text found
                        ExtractedText = string.Empty,
                        ExtractionMethod = "PDF (PdfPig)",
                        FileType = FileTypeCategory.Pdf,
                        WordCount = 0
                    };
                }

                _logger.LogInformation(
                    "PDF extraction complete: {WordCount} words from {FileName}",
                    extractedText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
                    fileName);

                return DocumentExtractionResult.FromPdf(extractedText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PDF extraction failed for: {FileName}", fileName);
                return DocumentExtractionResult.Failure(
                    $"Failed to extract text from PDF: {ex.Message}",
                    "PDF_EXTRACTION_FAILED"
                );
            }
        }

        /*
        =============================================================================
        IMAGE EXTRACTION (OCR MICROSERVICE)
        =============================================================================
        
        WWH:
        WHAT: Sends image to OCR microservice for text extraction
        WHY: Images need PaddleOCR which runs in Python microservice
        HOW: Calls _ocrService.ExtractTextFromImageAsync() which makes HTTP call
        
        =============================================================================
        */

        private async Task<DocumentExtractionResult> ExtractFromImageAsync(byte[] fileBytes, string fileName)
        {
            _logger.LogInformation("Sending image to OCR service: {FileName}", fileName);

            try
            {
                var ocrResult = await _ocrService.ExtractTextFromImageAsync(fileBytes, fileName);

                if (ocrResult.Success)
                {
                    _logger.LogInformation(
                        "OCR extraction complete: {WordCount} words, confidence: {Confidence}",
                        ocrResult.WordCount, ocrResult.Confidence);
                }
                else
                {
                    _logger.LogWarning(
                        "OCR extraction failed: {ErrorCode} - {ErrorMessage}",
                        ocrResult.ErrorCode, ocrResult.ErrorMessage);
                }

                return DocumentExtractionResult.FromOcr(ocrResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Image OCR failed for: {FileName}", fileName);
                return DocumentExtractionResult.Failure(
                    $"Failed to extract text from image: {ex.Message}",
                    "OCR_EXTRACTION_FAILED"
                );
            }
        }

        /*
        =============================================================================
        FILE TYPE DETECTION
        =============================================================================
        */

        public FileTypeCategory DetectFileType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (PdfExtensions.Contains(extension))
                return FileTypeCategory.Pdf;

            if (ImageExtensions.Contains(extension))
                return FileTypeCategory.Image;

            return FileTypeCategory.Unsupported;
        }

        public bool IsSupported(string fileName)
        {
            return DetectFileType(fileName) != FileTypeCategory.Unsupported;
        }

        public List<string> GetSupportedExtensions()
        {
            var extensions = new List<string>();
            extensions.AddRange(PdfExtensions);
            extensions.AddRange(ImageExtensions);
            return extensions;
        }
    }
}
