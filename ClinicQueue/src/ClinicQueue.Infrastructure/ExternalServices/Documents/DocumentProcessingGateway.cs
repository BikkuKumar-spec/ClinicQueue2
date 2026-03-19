using ClinicQueue.Application.Common;
using ClinicQueue.Application.DTOs;
using ClinicQueue.Application.Interfaces;
using ClinicQueue.Infrastructure.ExternalServices.OCR;
using ClinicQueue.Infrastructure.ExternalServices.PDF;

namespace ClinicQueue.Infrastructure.ExternalServices.Documents;

public class DocumentProcessingGateway(
    OcrDocumentProcessingGateway ocrGateway,
    PdfTextExtractionService pdfTextExtractionService) : IDocumentProcessingGateway
{
    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf"
    };

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".tif", ".tiff"
    };

    public async Task<Result<MedicalDocumentExtractionDto>> ExtractAsync(byte[] fileBytes, string fileName, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);

        if (PdfExtensions.Contains(extension))
        {
            return ExtractPdf(fileBytes);
        }

        if (ImageExtensions.Contains(extension))
        {
            return await ocrGateway.ExtractAsync(fileBytes, fileName, cancellationToken);
        }

        return Result<MedicalDocumentExtractionDto>.Failure($"Unsupported file type '{extension}'.");
    }

    private Result<MedicalDocumentExtractionDto> ExtractPdf(byte[] fileBytes)
    {
        try
        {
            var extractedText = pdfTextExtractionService.ExtractText(fileBytes);
            return Result<MedicalDocumentExtractionDto>.Success(
                new MedicalDocumentExtractionDto(
                    extractedText,
                    "PDF",
                    true,
                    null));
        }
        catch (Exception ex)
        {
            return Result<MedicalDocumentExtractionDto>.Failure($"PDF extraction failed: {ex.Message}");
        }
    }
}
