/*
=============================================================================
IOcrService.cs - INTERFACE FOR OCR MICROSERVICE COMMUNICATION
=============================================================================

WWH EXPLANATION:

WHAT:
    This is an INTERFACE - a contract that defines WHAT methods the OCR service
    must provide. It doesn't contain any implementation code, only method signatures.
    
    Think of it like a job description: it says what the service should do,
    but not HOW to do it.

WHY:
    * DEPENDENCY INJECTION: .NET's DI container uses interfaces to provide
      implementations at runtime. This allows swapping implementations easily.
    
    * TESTABILITY: You can create a mock/fake implementation for unit tests
      without needing the actual OCR microservice running.
    
    * LOOSE COUPLING: Your controllers and services depend on the interface,
      not the concrete class. If you change implementations, no other code breaks.
    
    * CLEAN ARCHITECTURE: Interfaces define boundaries between layers.
      Controllers don't need to know HOW OCR works - just that it works.

HOW:
    1. Define the interface with methods you need
    2. Create a concrete class that implements this interface (OcrService.cs)
    3. Register in Program.cs: builder.Services.AddHttpClient<IOcrService, OcrService>()
    4. Inject into controllers/services: public MyController(IOcrService ocrService)

=============================================================================
*/

namespace ClinicQueue.Services
{
    /// <summary>
    /// Interface for communicating with the OCR microservice.
    /// The OCR microservice extracts text from images using PaddleOCR.
    /// </summary>
    public interface IOcrService
    {
        /// <summary>
        /// Extracts text from an image file by sending it to the OCR microservice.
        /// </summary>
        /// <param name="imageBytes">The image file as a byte array</param>
        /// <param name="fileName">Original filename (used for content type detection)</param>
        /// <returns>
        /// OcrResult containing:
        /// - Success: whether extraction worked
        /// - ExtractedText: the text found in the image
        /// - Confidence: how confident the OCR is (0-1)
        /// - ErrorMessage: error details if failed
        /// </returns>
        Task<OcrResult> ExtractTextFromImageAsync(byte[] imageBytes, string fileName);

        /// <summary>
        /// Checks if the OCR microservice is running and healthy.
        /// </summary>
        /// <returns>True if service is available, false otherwise</returns>
        Task<bool> IsServiceHealthyAsync();

        /// <summary>
        /// Gets the list of image formats supported by the OCR service.
        /// </summary>
        /// <returns>List of supported file extensions (e.g., ".jpg", ".png")</returns>
        Task<List<string>> GetSupportedFormatsAsync();
    }

    /*
    =============================================================================
    OcrResult - DATA TRANSFER OBJECT (DTO) FOR OCR RESPONSES
    =============================================================================

    WWH EXPLANATION:

    WHAT:
        A DTO (Data Transfer Object) that carries OCR results between layers.
        It's a simple class with properties to hold data - no business logic.

    WHY:
        * CLEAN DATA STRUCTURE: Instead of returning multiple values or using
          out parameters, we return one object with all relevant data.
        
        * TYPE SAFETY: Strongly typed - compiler catches errors if you access
          wrong properties.
        
        * EASY TO EXTEND: Need to add more data? Add a property. Existing
          code still works.

    HOW:
        The OcrService creates this object after calling the microservice,
        and returns it to the caller.

    =============================================================================
    */

    /// <summary>
    /// Result of an OCR operation.
    /// </summary>
    public class OcrResult
    {
        /// <summary>
        /// Whether the OCR operation was successful.
        /// True = text was extracted (even if empty)
        /// False = an error occurred
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The text extracted from the image.
        /// Empty string if no text was found (but Success can still be true).
        /// </summary>
        public string ExtractedText { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score from 0.0 to 1.0.
        /// Higher = more confident the OCR read correctly.
        /// Null if not provided by the service.
        /// </summary>
        public double? Confidence { get; set; }

        /// <summary>
        /// Number of words detected in the image.
        /// </summary>
        public int? WordCount { get; set; }

        /// <summary>
        /// Error message if Success is false.
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Error code if Success is false (e.g., "INVALID_FILE", "SERVICE_UNAVAILABLE").
        /// </summary>
        public string? ErrorCode { get; set; }

        // ─── Static Factory Methods ──────────────────────────────────────────────
        // These make it easy to create success/failure results

        /// <summary>
        /// Creates a successful result with extracted text.
        /// </summary>
        public static OcrResult SuccessResult(string text, double? confidence = null, int? wordCount = null)
        {
            return new OcrResult
            {
                Success = true,
                ExtractedText = text,
                Confidence = confidence,
                WordCount = wordCount
            };
        }

        /// <summary>
        /// Creates a failed result with error information.
        /// </summary>
        public static OcrResult FailureResult(string errorMessage, string errorCode = "OCR_FAILED")
        {
            return new OcrResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                ErrorCode = errorCode
            };
        }
    }
}
