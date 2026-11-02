using UglyToad.PdfPig;
namespace WorkdayInvoiceAssistant;
public static class DocumentInspection
{
    public static (int Pages, string Kind) Inspect(byte[] bytes, string mime)
    {
        if (mime != "application/pdf") return (1,"Image — vision required");
        using var pdf=PdfDocument.Open(bytes);
        if (pdf.NumberOfPages is <1 or >50) throw new InvalidDataException("Use a PDF with 1–50 pages.");
        var empty=pdf.GetPages().Count(p=>string.IsNullOrWhiteSpace(p.Text));
        return (pdf.NumberOfPages, empty==pdf.NumberOfPages ? "Scanned PDF — vision required" : empty>0 ? "Mixed PDF — vision required for image pages" : "PDF with text — vision also available");
    }
}
