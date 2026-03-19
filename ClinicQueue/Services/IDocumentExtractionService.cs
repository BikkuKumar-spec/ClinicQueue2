/*
=============================================================================
IDocumentExtractionService.cs - UNIFIED DOCUMENT TEXT EXTRACTION
=============================================================================

WWH EXPLANATION:

WHAT:
    A service that extracts text from ANY uploaded document (PDF or image).
    It acts as a "facade" that hides the complexity of choosing between
    PDF extraction and OCR based on file type.

WHY:
    * SINGLE RESPONSIBILITY FOR CALLERS: Controllers just call ExtractTextAsync()
      without needing to know whether it's a PDF or image.
    
    * FILE TYPE DETECTION: Centralized logic for determining how to process a file.
    
    * EASY TO EXTEND: Want to add Word document support? Add it here without
      changing controllers.
    
    * CONSISTENT RESPONSE: Same result format regardless of extraction method.

HOW THE FLOW WORKS:

    ┌─────────────────────────────────────────────────────────────────────┐
    │                     DocumentExtractionService                        │
    ├─────────────────────────────────────────────────────────────────────┤
    │                                                                     │
    │   User Uploads File                                                 │
    │         │                                                           │
    │         ▼                                                           │
    │   ExtractTextAsync(file)                                            │
    │         │                                                           │
    │         ▼                                                           │
    │   DetectFileType(extension)                                         │
    │         │                                                           │
    │    ┌────┴────┐                                                      │
    │    │         │                                                      │
    │    ▼         ▼                                                      │
    │  PDF?      Image?                                                   │
    │    │         │                                                      │
    │    ▼         ▼                                                      │
    │ PdfExtract  OcrService                                              │
    │ Service     (HTTP to                                                │
    │ (Local)     Python)                                                 │
    │    │         │                                                      │
    │    └────┬────┘                                                      │
    │         │                                                           │
    │         ▼                                                           │
    │   DocumentExtractionResult                                          │
    │   (unified response)                                                │
    │                                                                     │
    └─────────────────────────────────────────────────────────────────────┘

=============================================================================
*/

namespace ClinicQueue.Services
{
    /// <summary>
    /// Service for extracting text from documents (PDFs and images).
    /// Automatically routes to the appropriate extraction method based on file type.
    /// </summary>
    public interface IDocumentExtractionService
    {
        /// <summary>
        /// Extracts text from a document file (PDF or image).
        /// Automatically detects file type and uses appropriate method.
        /// </summary>
        /// <param name="fileBytes">The file content as bytes</param>
        /// <param name="fileName">Original filename with extension</param>
        /// <returns>Extraction result with text and metadata</returns>
        Task<DocumentExtractionResult> ExtractTextAsync(byte[] fileBytes, string fileName);

        /// <summary>
        /// Checks if a file type is supported for text extraction.
        /// </summary>
        /// <param name="fileName">Filename with extension</param>
        /// <returns>True if the file can be processed</returns>
        bool IsSupported(string fileName);

        /// <summary>
        /// Gets the list of all supported file extensions.
        /// </summary>
        List<string> GetSupportedExtensions();

        /// <summary>
        /// Determines the file type category (PDF, Image, or Unsupported).
        /// </summary>
        FileTypeCategory DetectFileType(string fileName);
    }

    /// <summary>
    /// Categorizes uploaded files for routing to appropriate extraction service.
    /// </summary>
    public enum FileTypeCategory
    {
        /// <summary>PDF documents - processed locally with PdfPig</summary>
        Pdf,

        /// <summary>Image files - sent to OCR microservice</summary>
        Image,

        /// <summary>File type not supported for text extraction</summary>
        Unsupported
    }

    /// <summary>
    /// Result of document text extraction.
    /// Provides consistent structure regardless of extraction method.
    /// </summary>
    public class DocumentExtractionResult
    {
        /// <summary>Whether extraction was successful</summary>
        public bool Success { get; set; }

        /// <summary>The extracted text content</summary>
        public string ExtractedText { get; set; } = string.Empty;

        /// <summary>What method was used (PDF/OCR)</summary>
        public string ExtractionMethod { get; set; } = string.Empty;

        /// <summary>Original file type</summary>
        public FileTypeCategory FileType { get; set; }

        /// <summary>Confidence score (for OCR only, null for PDF)</summary>
        public double? Confidence { get; set; }

        /// <summary>Word count of extracted text</summary>
        public int WordCount { get; set; }

        /// <summary>Error message if extraction failed</summary>
        public string? ErrorMessage { get; set; }

        /// <summary>Error code if extraction failed</summary>
        public string? ErrorCode { get; set; }

        // ─── Factory Methods ─────────────────────────────────────────────────────

        public static DocumentExtractionResult FromPdf(string text)
        {
            return new DocumentExtractionResult
            {
                Success = true,
                ExtractedText = text,
                ExtractionMethod = "PDF (PdfPig)",
                FileType = FileTypeCategory.Pdf,
                WordCount = string.IsNullOrWhiteSpace(text) ? 0 : text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
            };
        }

        public static DocumentExtractionResult FromOcr(OcrResult ocrResult)
        {
            if (ocrResult.Success)
            {
                return new DocumentExtractionResult
                {
                    Success = true,
                    ExtractedText = ocrResult.ExtractedText,
                    ExtractionMethod = "OCR (PaddleOCR)",
                    FileType = FileTypeCategory.Image,
                    Confidence = ocrResult.Confidence,
                    WordCount = ocrResult.WordCount ?? 0
                };
            }
            else
            {
                return new DocumentExtractionResult
                {
                    Success = false,
                    ErrorMessage = ocrResult.ErrorMessage,
                    ErrorCode = ocrResult.ErrorCode,
                    ExtractionMethod = "OCR (PaddleOCR)",
                    FileType = FileTypeCategory.Image
                };
            }
        }

        public static DocumentExtractionResult Failure(string message, string code = "EXTRACTION_FAILED")
        {
            return new DocumentExtractionResult
            {
                Success = false,
                ErrorMessage = message,
                ErrorCode = code
            };
        }

        public static DocumentExtractionResult UnsupportedFormat(string extension)
        {
            return new DocumentExtractionResult
            {
                Success = false,
                ErrorMessage = $"Unsupported file format: {extension}",
                ErrorCode = "UNSUPPORTED_FORMAT",
                FileType = FileTypeCategory.Unsupported
            };
        }
    }
}
