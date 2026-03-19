using System.Text;
using UglyToad.PdfPig;

namespace ClinicQueue.Infrastructure.ExternalServices.PDF;

public class PdfTextExtractionService
{
    public string ExtractText(byte[] pdfBytes)
    {
        var builder = new StringBuilder();

        using var document = PdfDocument.Open(pdfBytes);
        foreach (var page in document.GetPages())
        {
            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                builder.AppendLine(page.Text);
            }
        }

        return builder.ToString().Trim();
    }
}
