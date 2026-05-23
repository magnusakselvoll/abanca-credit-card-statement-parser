namespace FireflyIiiCustomUploader.Core.Parsing;

public class StatementParserRegistry
{
    private readonly IReadOnlyList<IStatementParser> _parsers;

    public StatementParserRegistry(IEnumerable<IStatementParser> parsers)
    {
        _parsers = parsers.ToList();
    }

    public IReadOnlyList<IStatementParser> Parsers => _parsers;

    public IStatementParser? FindParser(IReadOnlyList<string> lines)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(lines))
                return parser;
        }
        return null;
    }

    public IStatementParser? GetParser(string formatId) =>
        _parsers.FirstOrDefault(p => p.FormatId == formatId);
}
