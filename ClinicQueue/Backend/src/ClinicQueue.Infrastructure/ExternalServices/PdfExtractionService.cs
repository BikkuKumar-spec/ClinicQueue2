using System.Text;
using UglyToad.PdfPig;

namespace ClinicQueue.Infrastructure.ExternalServices;

public interface IPdfExtractionService
{
    string ExtractTextFromPdf(byte[] pdfBytes);
}

public class PdfExtractionService : IPdfExtractionService
{
    public string ExtractTextFromPdf(byte[] pdfBytes)
    {
        var sb = new StringBuilder();

        using var document = PdfDocument.Open(pdfBytes);
        foreach (var page in document.GetPages())
        {
            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                sb.AppendLine(pageText);
            }
        }

        return sb.ToString().Trim();
    }
}
