namespace AbancaCardParser.Core.Parsing;

public interface IPdfTextExtractor
{
    IReadOnlyList<string> ExtractLines(Stream pdfStream);
}
