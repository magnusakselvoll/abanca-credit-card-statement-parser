using FireflyIiiCustomUploader.Core.Models;
using FireflyIiiCustomUploader.Core.Parsing.Advanzia;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class AdvanziaStatementParserTests
{
    private readonly AdvanziaStatementParser _parser = new();

    // Synthetic header lines: export-range gives the 4-digit year anchor; column header triggers CanParse.
    private static readonly string[] HeaderLines =
    [
        "Exported date range: January 1, 2025 - January 31, 2025",
        "Date Counterparty Description Category Amount",
    ];

    private IReadOnlyList<CardTransaction> ParseLines(params string[] transactionLines)
    {
        var all = new List<string>(HeaderLines);
        all.AddRange(transactionLines);
        return _parser.Parse(all).Transactions;
    }

    [TestMethod]
    public void Parse_DebitTransaction_IsDebitTrue()
    {
        var result = ParseLines("15.04.25 ACME STORE groceries € -42,50");

        Assert.HasCount(1, result);
        var tx = result[0];
        Assert.AreEqual(new DateOnly(2025, 4, 15), tx.Date);
        Assert.AreEqual("ACME STORE", tx.Description);
        Assert.AreEqual(42.50m, tx.Amount);
        Assert.IsTrue(tx.IsDebit);
    }

    [TestMethod]
    public void Parse_CreditTransaction_IsDebitFalse()
    {
        var result = ParseLines("05.04.25 SETTLEMENT BANK directdebit € 100,00");

        Assert.HasCount(1, result);
        var tx = result[0];
        Assert.AreEqual(new DateOnly(2025, 4, 5), tx.Date);
        Assert.IsFalse(tx.IsDebit);
        Assert.AreEqual(100.00m, tx.Amount);
    }

    [TestMethod]
    public void Parse_CategoryCaptured()
    {
        var result = ParseLines("15.04.25 ACME STORE groceries € -42,50");

        Assert.HasCount(1, result);
        Assert.AreEqual("groceries", result[0].Category);
    }

    [TestMethod]
    public void Parse_CategoryStrippedFromDescription()
    {
        var result = ParseLines("15.04.25 ACME STORE groceries € -42,50");

        Assert.HasCount(1, result);
        Assert.AreEqual("ACME STORE", result[0].Description);
    }

    [TestMethod]
    public void Parse_MultiWordMerchant_ParsedCorrectly()
    {
        var result = ParseLines("20.04.25 FOREIGN EXCHANGE FEE miscellaneous € -3,50");

        Assert.HasCount(1, result);
        Assert.AreEqual("FOREIGN EXCHANGE FEE", result[0].Description);
        Assert.AreEqual("miscellaneous", result[0].Category);
    }

    [TestMethod]
    public void Parse_ThousandsSeparatorInAmount()
    {
        var result = ParseLines("10.04.25 BIG WORLD TRAVEL travel € -1.234,56");

        Assert.HasCount(1, result);
        Assert.AreEqual(1234.56m, result[0].Amount);
    }

    [TestMethod]
    public void Parse_YearFromExportHeader()
    {
        var lines = new List<string>
        {
            "Exported date range: December 1, 2024 - December 31, 2024",
            "Date Counterparty Description Category Amount",
            "15.12.24 CORNER SHOP shopping € -8,99",
            "02.01.25 CORNER SHOP shopping € -8,99",
        };
        var result = _parser.Parse(lines).Transactions;

        Assert.HasCount(2, result);
        Assert.AreEqual(new DateOnly(2024, 12, 15), result[0].Date);
        Assert.AreEqual(new DateOnly(2025, 1, 2), result[1].Date);
    }

    [TestMethod]
    public void Parse_IgnoresNonTransactionLines()
    {
        var lines = new List<string>
        {
            "Dear Test User,",
            "Exported date range: April 1, 2025 - April 30, 2025",
            "Date Counterparty Description Category Amount",
            "",
            "15.04.25 PHARMACY PLUS health € -18,75",
            "Some footer text",
        };
        var result = _parser.Parse(lines).Transactions;

        Assert.HasCount(1, result);
        Assert.AreEqual("PHARMACY PLUS", result[0].Description);
    }

    [TestMethod]
    public void Parse_MultipleTransactions_PreservesOrder()
    {
        var result = ParseLines(
            "01.04.25 BAKERY SHOP food € -5,30",
            "02.04.25 GAS STATION fuel € -40,00",
            "03.04.25 SETTLEMENT BANK directdebit € 100,00"
        );

        Assert.HasCount(3, result);
        Assert.AreEqual(new DateOnly(2025, 4, 1), result[0].Date);
        Assert.AreEqual(new DateOnly(2025, 4, 2), result[1].Date);
        Assert.AreEqual(new DateOnly(2025, 4, 3), result[2].Date);
    }

    [TestMethod]
    public void Parse_StatedTotal_IsNull()
    {
        var result = _parser.Parse(new List<string>(HeaderLines)
        {
            "15.04.25 ACME STORE groceries € -42,50",
        });

        Assert.IsNull(result.StatedTotal);
    }

    [TestMethod]
    public void CanParse_WithAdvanziaColumnHeader_ReturnsTrue()
    {
        var lines = new List<string>
        {
            "Dear Test User,",
            "Exported date range: April 1, 2025 - April 30, 2025",
            "Date Counterparty Description Category Amount",
        };
        Assert.IsTrue(_parser.CanParse(lines));
    }

    [TestMethod]
    public void CanParse_WithoutAdvanziaMarker_ReturnsFalse()
    {
        var lines = new List<string> { "Some random PDF content", "No relevant marker here" };
        Assert.IsFalse(_parser.CanParse(lines));
    }
}
