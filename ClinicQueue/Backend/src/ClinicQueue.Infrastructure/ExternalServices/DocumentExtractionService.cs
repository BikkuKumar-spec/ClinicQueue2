namespace ClinicQueue.Infrastructure.ExternalServices;

public interface IDocumentExtractionService
{
    Task<DocumentExtractionResult> ExtractTextAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default);
    bool IsSupported(string fileName);
    List<string> GetSupportedExtensions();
    FileTypeCategory DetectFileType(string fileName);
}

public enum FileTypeCategory
{
    Pdf,
    Image,
    Unsupported
}

public class DocumentExtractionResult
{
    public bool Success { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public string ExtractionMethod { get; set; } = string.Empty;
    public FileTypeCategory FileType { get; set; }
    public double? Confidence { get; set; }
    public int WordCount { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }

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

        return new DocumentExtractionResult
        {
            Success = false,
            ErrorMessage = ocrResult.ErrorMessage,
            ErrorCode = ocrResult.ErrorCode,
            ExtractionMethod = "OCR (PaddleOCR)",
            FileType = FileTypeCategory.Image
        };
    }

    public static DocumentExtractionResult Failure(string message, string code = "EXTRACTION_FAILED")
    {
        return new DocumentExtractionResult { Success = false, ErrorMessage = message, ErrorCode = code };
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

public class DocumentExtractionService(IPdfExtractionService pdfService, IOcrService ocrService) : IDocumentExtractionService
{
    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf" };
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };

    public async Task<DocumentExtractionResult> ExtractTextAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        if (fileBytes is null || fileBytes.Length == 0)
        {
            return DocumentExtractionResult.Failure("File is empty or missing", "EMPTY_FILE");
        }

        if (string.IsNullOrWhiteSpace(fileName))
        {
            return DocumentExtractionResult.Failure("Filename is required", "MISSING_FILENAME");
        }

        var fileType = DetectFileType(fileName);
        return fileType switch
        {
            FileTypeCategory.Pdf => await ExtractFromPdfAsync(fileBytes),
            FileTypeCategory.Image => await ExtractFromImageAsync(fileBytes, fileName, cancellationToken),
            _ => DocumentExtractionResult.UnsupportedFormat(Path.GetExtension(fileName).ToLowerInvariant())
        };
    }

    private Task<DocumentExtractionResult> ExtractFromPdfAsync(byte[] fileBytes)
    {
        try
        {
            var extractedText = pdfService.ExtractTextFromPdf(fileBytes);
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return Task.FromResult(new DocumentExtractionResult
                {
                    Success = true,
                    ExtractedText = string.Empty,
                    ExtractionMethod = "PDF (PdfPig)",
                    FileType = FileTypeCategory.Pdf,
                    WordCount = 0
                });
            }

            return Task.FromResult(DocumentExtractionResult.FromPdf(extractedText));
        }
        catch (Exception ex)
        {
            return Task.FromResult(DocumentExtractionResult.Failure($"Failed to extract text from PDF: {ex.Message}", "PDF_EXTRACTION_FAILED"));
        }
    }

    private async Task<DocumentExtractionResult> ExtractFromImageAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken)
    {
        try
        {
            var ocrResult = await ocrService.ExtractTextFromImageAsync(fileBytes, fileName, cancellationToken);
            return DocumentExtractionResult.FromOcr(ocrResult);
        }
        catch (Exception ex)
        {
            return DocumentExtractionResult.Failure($"Failed to extract text from image: {ex.Message}", "OCR_EXTRACTION_FAILED");
        }
    }

    public FileTypeCategory DetectFileType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (PdfExtensions.Contains(extension)) return FileTypeCategory.Pdf;
        if (ImageExtensions.Contains(extension)) return FileTypeCategory.Image;
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
