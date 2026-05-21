namespace FireflyIiiCustomUploader.Core.Parsing;

public interface IPdfTextExtractor
{
    IReadOnlyList<string> ExtractLines(Stream pdfStream);
}
