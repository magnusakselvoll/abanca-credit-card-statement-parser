using FireflyIiiCustomUploader.Core.Models;

namespace FireflyIiiCustomUploader.Core.Parsing;

public interface IStatementParser
{
    string FormatId { get; }

    bool CanParse(IReadOnlyList<string> lines);

    CardStatement Parse(IReadOnlyList<string> lines);
}
