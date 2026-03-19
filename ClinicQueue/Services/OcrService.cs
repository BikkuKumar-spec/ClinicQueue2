/*
=============================================================================
OcrService.cs - HTTP CLIENT FOR OCR MICROSERVICE COMMUNICATION
=============================================================================

WWH EXPLANATION:

WHAT:
    This class implements IOcrService and handles actual HTTP communication
    with the Python OCR microservice. It:
    - Sends images to the microservice using multipart/form-data
    - Receives JSON responses
    - Parses results into OcrResult objects

WHY:
    * SEPARATION OF CONCERNS: All HTTP/network logic is in one place.
      Controllers don't need to know about HttpClient, multipart forms, etc.
    
    * ENCAPSULATION: If the OCR API changes, you only update this class.
    
    * ERROR HANDLING: Centralized handling of network errors, timeouts, etc.
    
    * TYPED HTTP CLIENT: .NET's AddHttpClient<IOcrService, OcrService>()
      provides a pre-configured HttpClient with proper lifecycle management.

HOW IT WORKS (STEP BY STEP):

    1. USER UPLOADS IMAGE
       └─> Controller receives IFormFile
    
    2. CONTROLLER CALLS THIS SERVICE
       └─> ExtractTextFromImageAsync(bytes, filename)
    
    3. THIS SERVICE BUILDS HTTP REQUEST
       └─> Creates MultipartFormDataContent
       └─> Adds file as "file" field
    
    4. SENDS TO PYTHON MICROSERVICE
       └─> POST http://localhost:8001/api/v1/extract-text
    
    5. PARSES JSON RESPONSE
       └─> Deserializes to OcrApiResponse
    
    6. RETURNS OcrResult
       └─> Success/failure with extracted text

=============================================================================
*/

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ClinicQueue.Services
{
    /// <summary>
    /// Service for communicating with the OCR microservice.
    /// Uses HttpClient to send images and receive extracted text.
    /// </summary>
    public class OcrService : IOcrService
    {
        // ─── Dependencies (Injected via Constructor) ─────────────────────────────

        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly ILogger<OcrService> _logger;

        // ─── JSON Serialization Options ──────────────────────────────────────────

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,  // "extracted_text" matches "ExtractedText"
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower  // .NET 8+ feature
        };

        // ─── Supported Image Extensions ──────────────────────────────────────────

        private static readonly HashSet<string> _supportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif"
        };

        /*
        =============================================================================
        CONSTRUCTOR
        =============================================================================
        
        WWH:
        WHAT: Initializes the service with dependencies
        WHY: .NET DI provides HttpClient, config, and logger automatically
        HOW: Parameters are injected by the DI container when service is created
        
        =============================================================================
        */

        public OcrService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<OcrService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Read URL from appsettings.json, default to localhost:8001
            _baseUrl = configuration["AI:OcrServiceUrl"] ?? "http://localhost:8001";

            _logger.LogInformation("OCR Service initialized with base URL: {BaseUrl}", _baseUrl);
        }

        /*
        =============================================================================
        EXTRACT TEXT FROM IMAGE - MAIN METHOD
        =============================================================================
        
        WWH:
        WHAT: Sends an image to the OCR microservice and returns extracted text
        
        WHY: This is the core functionality - taking an image and getting text out
        
        HOW (DETAILED STEPS):
            1. Validate the file extension is supported
            2. Create a MultipartFormDataContent object (like a web form)
            3. Add the image bytes as a file field named "file"
            4. Set the correct Content-Type header based on extension
            5. POST to /api/v1/extract-text endpoint
            6. Read and parse the JSON response
            7. Return OcrResult with success/failure
        
        =============================================================================
        */

        public async Task<OcrResult> ExtractTextFromImageAsync(byte[] imageBytes, string fileName)
        {
            _logger.LogInformation("Starting OCR extraction for file: {FileName} ({Size} bytes)",
                fileName, imageBytes.Length);

            // ─── STEP 1: Validate File Extension ─────────────────────────────────

            var extension = Path.GetExtension(fileName).ToLowerInvariant();

            if (!_supportedExtensions.Contains(extension))
            {
                _logger.LogWarning("Unsupported file extension: {Extension}", extension);
                return OcrResult.FailureResult(
                    $"Unsupported image format '{extension}'. Supported: jpg, jpeg, png, bmp, tiff",
                    "UNSUPPORTED_FORMAT"
                );
            }

            try
            {
                // ─── STEP 2: Create Multipart Form Content ───────────────────────
                /*
                WWH:
                WHAT: MultipartFormDataContent is how you send files via HTTP
                WHY: The OCR API expects multipart/form-data (standard for file uploads)
                HOW: Create container, add file content with proper headers
                */

                using var formContent = new MultipartFormDataContent();

                // Create content from bytes
                var fileContent = new ByteArrayContent(imageBytes);

                // Set the Content-Type header based on file extension
                var contentType = GetContentType(extension);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                // Add as "file" field (must match what Python FastAPI expects)
                formContent.Add(fileContent, "file", fileName);

                // ─── STEP 3: Send HTTP POST Request ──────────────────────────────
                /*
                WWH:
                WHAT: HTTP POST with the form data to the OCR endpoint
                WHY: This is how we communicate with the Python microservice
                HOW: HttpClient.PostAsync sends request, returns response
                */

                _logger.LogDebug("Sending POST to {Url}", $"{_baseUrl}/api/v1/extract-text");

                var response = await _httpClient.PostAsync(
                    $"{_baseUrl}/api/v1/extract-text",
                    formContent
                );

                // ─── STEP 4: Read Response Content ───────────────────────────────

                var responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("OCR Response: {StatusCode} - {Body}",
                    response.StatusCode, responseBody);

                // ─── STEP 5: Handle Non-Success Status Codes ─────────────────────
                /*
                WWH:
                WHAT: Check if the HTTP request succeeded (2xx status)
                WHY: The microservice might return 400 (bad request), 500 (error), etc.
                HOW: Parse error response or use status code
                */

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("OCR service returned error: {StatusCode} - {Body}",
                        response.StatusCode, responseBody);

                    // Try to parse error response
                    try
                    {
                        var errorResponse = JsonSerializer.Deserialize<OcrApiResponse>(
                            responseBody, _jsonOptions);

                        return OcrResult.FailureResult(
                            errorResponse?.Message ?? $"OCR service error: {response.StatusCode}",
                            errorResponse?.Error ?? "SERVICE_ERROR"
                        );
                    }
                    catch
                    {
                        return OcrResult.FailureResult(
                            $"OCR service error: {response.StatusCode}",
                            "SERVICE_ERROR"
                        );
                    }
                }

                // ─── STEP 6: Parse Success Response ──────────────────────────────
                /*
                WWH:
                WHAT: Deserialize JSON response to strongly-typed object
                WHY: Easier to work with typed objects than raw JSON strings
                HOW: JsonSerializer.Deserialize<T>() converts JSON to C# object
                */

                var ocrResponse = JsonSerializer.Deserialize<OcrApiResponse>(
                    responseBody, _jsonOptions);

                if (ocrResponse == null)
                {
                    _logger.LogError("Failed to parse OCR response: {Body}", responseBody);
                    return OcrResult.FailureResult(
                        "Failed to parse OCR service response",
                        "PARSE_ERROR"
                    );
                }

                // ─── STEP 7: Return Success Result ───────────────────────────────

                if (ocrResponse.Success)
                {
                    _logger.LogInformation(
                        "OCR extraction successful: {WordCount} words, confidence: {Confidence}",
                        ocrResponse.WordCount, ocrResponse.Confidence);

                    return OcrResult.SuccessResult(
                        ocrResponse.ExtractedText ?? string.Empty,
                        ocrResponse.Confidence,
                        ocrResponse.WordCount
                    );
                }
                else
                {
                    return OcrResult.FailureResult(
                        ocrResponse.Message ?? "OCR extraction failed",
                        ocrResponse.Error ?? "OCR_FAILED"
                    );
                }
            }
            catch (HttpRequestException ex)
            {
                // Network error - service might be down
                _logger.LogError(ex, "HTTP error calling OCR service");
                return OcrResult.FailureResult(
                    $"Could not connect to OCR service: {ex.Message}",
                    "SERVICE_UNAVAILABLE"
                );
            }
            catch (TaskCanceledException ex)
            {
                // Timeout
                _logger.LogError(ex, "OCR service request timed out");
                return OcrResult.FailureResult(
                    "OCR service request timed out",
                    "TIMEOUT"
                );
            }
            catch (Exception ex)
            {
                // Unexpected error
                _logger.LogError(ex, "Unexpected error calling OCR service");
                return OcrResult.FailureResult(
                    $"Unexpected error: {ex.Message}",
                    "UNEXPECTED_ERROR"
                );
            }
        }

        /*
        =============================================================================
        HEALTH CHECK - CHECK IF OCR SERVICE IS RUNNING
        =============================================================================
        */

        public async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/health");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OCR service health check failed");
                return false;
            }
        }

        /*
        =============================================================================
        GET SUPPORTED FORMATS
        =============================================================================
        */

        public async Task<List<string>> GetSupportedFormatsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/supported-formats");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<SupportedFormatsResponse>(
                        content, _jsonOptions);
                    return result?.SupportedFormats ?? _supportedExtensions.ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get supported formats from OCR service");
            }

            // Return default list if service call fails
            return _supportedExtensions.ToList();
        }

        /*
        =============================================================================
        HELPER: GET CONTENT TYPE FROM FILE EXTENSION
        =============================================================================
        
        WWH:
        WHAT: Maps file extensions to MIME types
        WHY: HTTP requires proper Content-Type headers for the server to understand the file
        HOW: Simple switch/case based on extension
        
        =============================================================================
        */

        private static string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".bmp" => "image/bmp",
                ".tiff" or ".tif" => "image/tiff",
                _ => "application/octet-stream"  // Fallback
            };
        }

        /*
        =============================================================================
        HELPER: CHECK IF FILE IS SUPPORTED IMAGE
        =============================================================================
        */

        public static bool IsSupportedImageFormat(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return _supportedExtensions.Contains(extension);
        }
    }

    /*
    =============================================================================
    API RESPONSE MODELS - FOR JSON DESERIALIZATION
    =============================================================================
    
    WWH:
    WHAT: These classes match the JSON structure from the Python OCR microservice
    WHY: JsonSerializer needs classes to deserialize JSON into
    HOW: Property names match JSON keys (with snake_case -> PascalCase mapping)
    
    Python response:
    {
        "success": true,
        "extracted_text": "Patient Name: John Doe",
        "confidence": 0.92,
        "word_count": 5
    }
    
    C# class properties match these (with JsonPropertyName for snake_case)
    
    =============================================================================
    */

    /// <summary>
    /// Maps to the JSON response from POST /api/v1/extract-text
    /// </summary>
    internal class OcrApiResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("extracted_text")]
        public string? ExtractedText { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }

        [JsonPropertyName("word_count")]
        public int? WordCount { get; set; }

        // Error fields (returned when success is false)
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }

    /// <summary>
    /// Maps to the JSON response from GET /api/v1/supported-formats
    /// </summary>
    internal class SupportedFormatsResponse
    {
        [JsonPropertyName("supported_formats")]
        public List<string>? SupportedFormats { get; set; }
    }
}
