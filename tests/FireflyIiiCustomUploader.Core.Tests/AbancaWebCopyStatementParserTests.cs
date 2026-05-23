using FireflyIiiCustomUploader.Core.Models;
using FireflyIiiCustomUploader.Core.Parsing.Abanca;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class AbancaWebCopyStatementParserTests
{
    private readonly AbancaWebCopyStatementParser _parser = new();

    private IReadOnlyList<CardTransaction> ParseLines(params string[] lines) =>
        _parser.Parse(lines).Transactions;

    private static string Row(string tipo, string concepto, string importe,
        string date = "01/05/2026", string situacion = "Liquidado") =>
        $"TIT.\t{date}\t{tipo}\t{situacion}\t{concepto}\t{importe}";

    [TestMethod]
    public void Parse_DebitTransaction_IsDebitTrue()
    {
        var result = ParseLines(Row("FRA. VENTA", "METRO DE MALAGA\\CAMPANILL", "-7,84 EUR"));

        Assert.HasCount(1, result);
        var tx = result[0];
        Assert.AreEqual(new DateOnly(2026, 5, 1), tx.Date);
        Assert.AreEqual("METRO DE MALAGA\\CAMPANILL", tx.Description);
        Assert.AreEqual(7.84m, tx.Amount);
        Assert.IsTrue(tx.IsDebit);
    }

    [TestMethod]
    public void Parse_CreditTransaction_IsDebitFalse()
    {
        var result = ParseLines(Row("FRA. VENTA", "PAYPAL REFUND", "50,00 EUR"));

        Assert.HasCount(1, result);
        Assert.IsFalse(result[0].IsDebit);
        Assert.AreEqual(50.00m, result[0].Amount);
    }

    [TestMethod]
    public void Parse_AmortizacionDeuda_ParsedAsRegularCredit()
    {
        var result = ParseLines(Row("AMORTIZACION DEUDA", "AMORTIZACION DEUDA", "119,07 EUR",
            date: "30/04/2026"));

        Assert.HasCount(1, result);
        var tx = result[0];
        Assert.IsFalse(tx.IsDebit);
        Assert.AreEqual(119.07m, tx.Amount);
        Assert.AreEqual("AMORTIZACION DEUDA", tx.Description);
        Assert.AreEqual("AMORTIZACION DEUDA", tx.Category);
    }

    [TestMethod]
    public void Parse_CategoryPopulatedFromTipoOperacion()
    {
        var result = ParseLines(Row("FRA. VENTA", "AWS EMEA", "-0,86 EUR"));

        Assert.HasCount(1, result);
        Assert.AreEqual("FRA. VENTA", result[0].Category);
    }

    [TestMethod]
    public void Parse_DescriptionFromConcepto()
    {
        var result = ParseLines(Row("FRA. VENTA", "PAYPAL *ALIPAY EUR", "-26,31 EUR",
            date: "27/04/2026"));

        Assert.HasCount(1, result);
        Assert.AreEqual("PAYPAL *ALIPAY EUR", result[0].Description);
    }

    [TestMethod]
    public void Parse_Date_ParsedCorrectly()
    {
        var result = ParseLines(Row("FRA. VENTA", "SHOP", "-10,00 EUR", date: "30/04/2026"));

        Assert.HasCount(1, result);
        Assert.AreEqual(new DateOnly(2026, 4, 30), result[0].Date);
    }

    [TestMethod]
    public void Parse_HeaderRow_Ignored()
    {
        var lines = new[]
        {
            "\tF.OPERAC.\tTIPO OPERACIÓN\tSITUACIÓN\tCONCEPTO\tIMPORTE",
            Row("FRA. VENTA", "GROCERY STORE", "-25,00 EUR"),
        };
        var result = _parser.Parse(lines).Transactions;

        Assert.HasCount(1, result);
    }

    [TestMethod]
    public void Parse_MultipleRows_AllParsed()
    {
        var result = ParseLines(
            Row("AMORTIZACION DEUDA", "AMORTIZACION DEUDA", "119,07 EUR", date: "30/04/2026"),
            Row("FRA. VENTA", "METRO DE MALAGA\\CAMPANILL", "-7,84 EUR"),
            Row("FRA. VENTA", "AWS EMEA", "-0,86 EUR"),
            Row("FRA. VENTA", "PAYPAL *ALIPAY EUR", "-26,31 EUR", date: "27/04/2026")
        );

        Assert.HasCount(4, result);
        Assert.IsFalse(result[0].IsDebit);
        Assert.IsTrue(result[1].IsDebit);
        Assert.IsTrue(result[2].IsDebit);
        Assert.IsTrue(result[3].IsDebit);
    }

    [TestMethod]
    public void Parse_StatedTotal_IsNull()
    {
        var statement = _parser.Parse([Row("FRA. VENTA", "SHOP", "-10,00 EUR")]);
        Assert.IsNull(statement.StatedTotal);
    }

    [TestMethod]
    public void Parse_ThousandsSeparatorInAmount()
    {
        var result = ParseLines(Row("FRA. VENTA", "AIRLINE", "-1.234,56 EUR"));

        Assert.HasCount(1, result);
        Assert.AreEqual(1234.56m, result[0].Amount);
    }

    [TestMethod]
    public void Parse_EmptyInput_ReturnsNoTransactions()
    {
        var result = _parser.Parse([]).Transactions;
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Parse_LinesWithFewerThanSixFields_Ignored()
    {
        var result = ParseLines("TIT.\t01/05/2026\tFRA. VENTA");
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void CanParse_WithDataRow_ReturnsTrue()
    {
        var lines = new[]
        {
            "\tF.OPERAC.\tTIPO OPERACIÓN\tSITUACIÓN\tCONCEPTO\tIMPORTE",
            Row("FRA. VENTA", "SHOP", "-25,00 EUR"),
        };
        Assert.IsTrue(_parser.CanParse(lines));
    }

    [TestMethod]
    public void CanParse_WithoutDataRow_ReturnsFalse()
    {
        var lines = new[] { "Some random text", "No relevant content here" };
        Assert.IsFalse(_parser.CanParse(lines));
    }

    [TestMethod]
    public void CanParse_AbancaPdfLines_ReturnsFalse()
    {
        var lines = new[]
        {
            "2 15-05-2025 20-04-2025 A 19-05-2025 45072-1234",
            "15-04-25 00 SUPERMARKET 42,50 D",
            "TOTAL OPERACIONES TARJETA **** **** 1234 42,50",
        };
        Assert.IsFalse(_parser.CanParse(lines));
    }

    [TestMethod]
    public void CanParse_AdvanziaLines_ReturnsFalse()
    {
        var lines = new[]
        {
            "Date Counterparty Category € Amount",
            "15.04.25 SHOP GROCERIES € 42,50",
        };
        Assert.IsFalse(_parser.CanParse(lines));
    }

    [TestMethod]
    public void FormatId_IsExpected()
    {
        Assert.AreEqual("abanca-web-copy", _parser.FormatId);
    }

    [TestMethod]
    public void DisplayName_IsExpected()
    {
        Assert.AreEqual("Abanca credit card web copy", _parser.DisplayName);
    }

    [TestMethod]
    public void AccountNameHint_MatchesAbancaCreditAccounts()
    {
        Assert.IsNotNull(_parser.AccountNameHint);
        Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(
            "Abanca VISA credit card", _parser.AccountNameHint,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }
}
