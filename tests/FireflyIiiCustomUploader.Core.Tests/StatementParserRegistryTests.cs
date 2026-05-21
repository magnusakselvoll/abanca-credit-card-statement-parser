using FireflyIiiCustomUploader.Core.Models;
using FireflyIiiCustomUploader.Core.Parsing;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class StatementParserRegistryTests
{
    private sealed class AlwaysParser(string formatId) : IStatementParser
    {
        public string FormatId => formatId;
        public bool CanParse(IReadOnlyList<string> lines) => true;
        public CardStatement Parse(IReadOnlyList<string> lines) => new([], null);
    }

    private sealed class NeverParser : IStatementParser
    {
        public string FormatId => "never";
        public bool CanParse(IReadOnlyList<string> lines) => false;
        public CardStatement Parse(IReadOnlyList<string> lines) => throw new NotImplementedException();
    }

    [TestMethod]
    public void FindParser_MatchingParser_ReturnsIt()
    {
        var parser = new AlwaysParser("test");
        var registry = new StatementParserRegistry([parser]);

        var result = registry.FindParser([]);

        Assert.AreSame(parser, result);
    }

    [TestMethod]
    public void FindParser_NoMatchingParser_ReturnsNull()
    {
        var registry = new StatementParserRegistry([new NeverParser()]);

        var result = registry.FindParser([]);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindParser_EmptyRegistry_ReturnsNull()
    {
        var registry = new StatementParserRegistry([]);

        var result = registry.FindParser(["some line"]);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindParser_FirstMatchingParserWins()
    {
        var first = new AlwaysParser("first");
        var second = new AlwaysParser("second");
        var registry = new StatementParserRegistry([first, second]);

        var result = registry.FindParser([]);

        Assert.AreEqual("first", result!.FormatId);
    }

    [TestMethod]
    public void FindParser_SkipsNonMatchingParsers()
    {
        var parser = new AlwaysParser("match");
        var registry = new StatementParserRegistry([new NeverParser(), parser]);

        var result = registry.FindParser([]);

        Assert.AreEqual("match", result!.FormatId);
    }
}
