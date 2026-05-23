using FireflyIiiCustomUploader.Core.Models;
using FireflyIiiCustomUploader.Core.Parsing;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class StatementParserRegistryTests
{
    private sealed class AlwaysParser(string formatId) : IStatementParser
    {
        public string FormatId => formatId;
        public string DisplayName => formatId;
        public string? AccountNameHint => null;
        public bool CanParse(IReadOnlyList<string> lines) => true;
        public CardStatement Parse(IReadOnlyList<string> lines) => new([], null);
    }

    private sealed class NeverParser : IStatementParser
    {
        public string FormatId => "never";
        public string DisplayName => "Never";
        public string? AccountNameHint => null;
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

    [TestMethod]
    public void GetParser_KnownFormatId_ReturnsParser()
    {
        var parser = new AlwaysParser("test");
        var registry = new StatementParserRegistry([parser]);

        var result = registry.GetParser("test");

        Assert.AreSame(parser, result);
    }

    [TestMethod]
    public void GetParser_UnknownFormatId_ReturnsNull()
    {
        var registry = new StatementParserRegistry([new AlwaysParser("test")]);

        var result = registry.GetParser("unknown");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetParser_EmptyRegistry_ReturnsNull()
    {
        var registry = new StatementParserRegistry([]);

        var result = registry.GetParser("test");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Parsers_ReturnsAllRegisteredParsers()
    {
        var a = new AlwaysParser("a");
        var b = new AlwaysParser("b");
        var registry = new StatementParserRegistry([a, b]);

        Assert.HasCount(2, registry.Parsers);
        Assert.AreSame(a, registry.Parsers[0]);
        Assert.AreSame(b, registry.Parsers[1]);
    }
}
