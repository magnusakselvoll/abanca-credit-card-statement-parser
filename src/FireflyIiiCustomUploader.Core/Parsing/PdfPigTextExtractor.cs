using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace FireflyIiiCustomUploader.Core.Parsing;

public class PdfPigTextExtractor : IPdfTextExtractor
{
    public IReadOnlyList<string> ExtractLines(Stream pdfStream)
    {
        using var document = PdfDocument.Open(pdfStream);
        var lines = new List<string>();

        foreach (var page in document.GetPages())
        {
            // Group words by Y coordinate to reconstruct logical lines.
            // PDF Y-axis is bottom-up, so we sort descending to get top-to-bottom order.
            var wordGroups = page.GetWords()
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .GroupBy(w => (int)Math.Round(w.BoundingBox.Bottom))
                .OrderByDescending(g => g.Key)
                .Select(g => string.Join(" ", g.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));

            lines.AddRange(wordGroups);
        }

        return lines;
    }
}
