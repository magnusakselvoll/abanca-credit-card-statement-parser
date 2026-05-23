using FireflyIiiCustomUploader.Core.Models;
using FireflyIiiCustomUploader.Core.Parsing.Abanca;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class AbancaStatementParserTests
{
    private readonly AbancaStatementParser _parser = new();

    // Synthetic header lines that appear on every statement page.
    private static readonly string[] HeaderLines =
    [
        "2 15-05-2025 20-04-2025 A 19-05-2025 45072-1234",
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
        var result = ParseLines("15-04-25 00 SUPERMARKET DOWNTOWN 42,50 D");

        Assert.HasCount(1, result);
        var tx = result[0];
        Assert.AreEqual(new DateOnly(2025, 4, 15), tx.Date);
        Assert.AreEqual("SUPERMARKET DOWNTOWN", tx.Description);
        Assert.AreEqual(42.50m, tx.Amount);
        Assert.IsTrue(tx.IsDebit);
    }

    [TestMethod]
    public void Parse_CreditTransaction_IsDebitFalse()
    {
        var result = ParseLines("20-04-25 00 HOTEL REFUND 80,00 H");

        Assert.HasCount(1, result);
        var tx = result[0];
        Assert.AreEqual(new DateOnly(2025, 4, 20), tx.Date);
        Assert.IsFalse(tx.IsDebit);
    }

    [TestMethod]
    public void Parse_AmortizacionDeuda_ParsedAsRegularCredit()
    {
        // AMORTIZACION DEUDA may have a leading side-label character (e.g. "K") at the
        // same Y coordinate due to the "Mod. KQ0" print on the statement form.
        var result = ParseLines("K 30-04-25 00 AMORTIZACION DEUDA 500,00 H");

        Assert.HasCount(1, result);
        var tx = result[0];
        Assert.IsFalse(tx.IsDebit);
        Assert.AreEqual(500.00m, tx.Amount);
        Assert.AreEqual("AMORTIZACION DEUDA", tx.Description);
    }

    [TestMethod]
    public void Parse_LeadingNonDigitCharacters_AreTolerated()
    {
        // The vertical "Mod. KQ0" side label may prefix any transaction row.
        var result = ParseLines("K 15-04-25 00 GROCERY STORE 55,30 D");

        Assert.HasCount(1, result);
        Assert.AreEqual("GROCERY STORE", result[0].Description);
    }

    [TestMethod]
    public void Parse_ThousandsSeparatorInAmount()
    {
        var result = ParseLines("05-04-25 00 AIRLINE TICKETS 1.234,56 D");

        Assert.HasCount(1, result);
        Assert.AreEqual(1234.56m, result[0].Amount);
    }

    [TestMethod]
    public void Parse_TwoDigitYear_ExpandedToCurrentCentury()
    {
        // A statement spanning Dec/Jan — both years still resolve correctly within the current century.
        var lines = new List<string>
        {
            "2 15-01-2025 20-12-2024 A 19-01-2025 45072-1234",
            "31-12-24 00 ONLINE SHOP 29,99 D",
            "02-01-25 00 COFFEE SHOP 3,50 D",
        };
        var result = _parser.Parse(lines).Transactions;

        Assert.HasCount(2, result);
        Assert.AreEqual(new DateOnly(2024, 12, 31), result[0].Date);
        Assert.AreEqual(new DateOnly(2025, 1, 2), result[1].Date);
    }

    [TestMethod]
    public void Parse_TotalOperaciones_ExtractedCorrectly()
    {
        var lines = new List<string>
        {
            "2 15-05-2025 20-04-2025 A 19-05-2025 45072-1234",
            "15-04-25 00 RESTAURANT 30,00 D",
            "16-04-25 00 BOOKSTORE 20,00 D",
            "TOTAL OPERACIONES TARJETA **** **** 1234 50,00",
        };
        var statement = _parser.Parse(lines);

        Assert.HasCount(2, statement.Transactions);
        Assert.AreEqual(50.00m, statement.StatedTotal);
    }

    [TestMethod]
    public void Parse_NoHTransactions_HandledCorrectly()
    {
        var result = ParseLines(
            "01-04-25 00 GROCERY STORE 55,30 D",
            "02-04-25 00 GAS STATION 40,00 D"
        );

        Assert.HasCount(2, result);
        Assert.IsTrue(result.All(t => t.IsDebit));
    }

    [TestMethod]
    public void Parse_MultipleHTransactions_AllParsed()
    {
        var result = ParseLines(
            "30-04-25 00 AMORTIZACION DEUDA 500,00 H",
            "15-04-25 00 PARTIAL PAYMENT 200,00 H",
            "10-04-25 00 SHOP 100,00 D"
        );

        Assert.HasCount(3, result);
        Assert.AreEqual(2, result.Count(t => !t.IsDebit));
    }

    [TestMethod]
    public void Parse_IgnoresNonTransactionLines()
    {
        var lines = new List<string>
        {
            "EXTRACTO TARJETA DE CREDITO",
            "HOJA N FECHA COBRO PERIODO DE LIQUIDACION",
            "2 15-05-2025 20-04-2025 A 19-05-2025 45072-1234",
            "FECHA TARJ. CONCEPTO MOVIMIENTOS COMISIONES",
            "15-04-25 00 PHARMACY 18,75 D",
            "",
            "ABANCA Corporacion Bancaria",
        };
        var result = _parser.Parse(lines).Transactions;

        Assert.HasCount(1, result);
        Assert.AreEqual("PHARMACY", result[0].Description);
    }

    [TestMethod]
    public void Parse_DescriptionWithNumbers_ParsedCorrectly()
    {
        // Reference numbers at end of description should not be confused with the amount.
        var result = ParseLines("18-04-25 00 PAYPAL *MERCHANT 987654321 49,99 D");

        Assert.HasCount(1, result);
        Assert.AreEqual("PAYPAL *MERCHANT 987654321", result[0].Description);
        Assert.AreEqual(49.99m, result[0].Amount);
    }

    [TestMethod]
    public void Parse_MultipleTransactions_PreservesOrder()
    {
        var result = ParseLines(
            "01-04-25 00 GROCERY STORE 55,30 D",
            "02-04-25 00 GAS STATION 40,00 D",
            "03-04-25 00 PARTIAL REFUND 10,00 H"
        );

        Assert.HasCount(3, result);
        Assert.AreEqual(new DateOnly(2025, 4, 1), result[0].Date);
        Assert.AreEqual(new DateOnly(2025, 4, 2), result[1].Date);
        Assert.AreEqual(new DateOnly(2025, 4, 3), result[2].Date);
    }

    [TestMethod]
    public void Parse_StopsAtTotalOperaciones()
    {
        var lines = new List<string>
        {
            "2 15-05-2025 20-04-2025 A 19-05-2025 45072-1234",
            "10-04-25 00 FIRST PURCHASE 100,00 D",
            "TOTAL OPERACIONES TARJETA **** **** 1234 100,00",
            "11-04-25 00 THIS SHOULD BE IGNORED 50,00 D",
        };
        var result = _parser.Parse(lines).Transactions;

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public void ParseSpanishDecimal_HandlesVariousFormats()
    {
        Assert.AreEqual(9.99m, AbancaStatementParser.ParseSpanishDecimal("9,99"));
        Assert.AreEqual(1234.56m, AbancaStatementParser.ParseSpanishDecimal("1.234,56"));
        Assert.AreEqual(100.00m, AbancaStatementParser.ParseSpanishDecimal("100,00"));
        Assert.AreEqual(0.50m, AbancaStatementParser.ParseSpanishDecimal("0,50"));
    }

    [TestMethod]
    public void CanParse_WithAbancaMarker_ReturnsTrue()
    {
        var lines = new List<string>
        {
            "Some header",
            "TOTAL OPERACIONES TARJETA **** **** 1234 100,00",
        };
        Assert.IsTrue(_parser.CanParse(lines));
    }

    [TestMethod]
    public void CanParse_WithoutAbancaMarker_ReturnsFalse()
    {
        var lines = new List<string> { "Some random PDF", "No relevant marker here" };
        Assert.IsFalse(_parser.CanParse(lines));
    }

    [TestMethod]
    public void DisplayName_IsExpected()
    {
        Assert.AreEqual("Abanca credit card statement", _parser.DisplayName);
    }

    [TestMethod]
    public void AccountNameHint_MatchesAbancaCreditAccounts()
    {
        Assert.IsNotNull(_parser.AccountNameHint);
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
            "Abanca VISA credit card", _parser.AccountNameHint, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }
}
