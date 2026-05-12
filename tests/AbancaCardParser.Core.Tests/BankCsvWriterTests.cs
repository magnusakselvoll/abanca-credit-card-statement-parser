using AbancaCardParser.Core.Models;
using AbancaCardParser.Core.Output;

namespace AbancaCardParser.Core.Tests;

[TestClass]
public class BankCsvWriterTests
{
    private readonly BankCsvWriter _writer = new();

    private static CardStatement Statement(params CardTransaction[] transactions) =>
        new(transactions, null);

    [TestMethod]
    public void Write_Header_MatchesAbancaBankFormat()
    {
        var csv = _writer.Write(Statement());
        var firstLine = csv.Split("\r\n")[0];
        Assert.AreEqual("Fecha ctble;Fecha valor;Concepto;Importe;Moneda;Saldo;Moneda;Concepto ampliado", firstLine);
    }

    [TestMethod]
    public void Write_LineEndings_AreCrlf()
    {
        var csv = _writer.Write(Statement());
        Assert.IsTrue(csv.Contains("\r\n"));
        Assert.IsFalse(csv.Replace("\r\n", "").Contains('\r'));
    }

    [TestMethod]
    public void Write_DebitTransaction_NegativeImporte()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 15), "GROCERY STORE", 55.30m, IsDebit: true);
        var csv = _writer.Write(Statement(tx));
        var dataLine = csv.Split("\r\n")[1];
        Assert.AreEqual("15-04-2025;15-04-2025;GROCERY STORE;-55,30;EUR;;EUR;", dataLine);
    }

    [TestMethod]
    public void Write_CreditTransaction_PositiveImporte()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 20), "HOTEL REFUND", 80.00m, IsDebit: false);
        var csv = _writer.Write(Statement(tx));
        var dataLine = csv.Split("\r\n")[1];
        Assert.AreEqual("20-04-2025;20-04-2025;HOTEL REFUND;80,00;EUR;;EUR;", dataLine);
    }

    [TestMethod]
    public void Write_AmortizacionDeuda_PositiveImporte()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 30), "AMORTIZACION DEUDA", 500.00m, IsDebit: false);
        var csv = _writer.Write(Statement(tx));
        var dataLine = csv.Split("\r\n")[1];
        Assert.AreEqual("30-04-2025;30-04-2025;AMORTIZACION DEUDA;500,00;EUR;;EUR;", dataLine);
    }

    [TestMethod]
    public void Write_BothDateColumns_Equal()
    {
        var tx = new CardTransaction(new DateOnly(2025, 12, 31), "LAST DAY SHOP", 10.00m, IsDebit: true);
        var csv = _writer.Write(Statement(tx));
        var parts = csv.Split("\r\n")[1].Split(';');
        Assert.AreEqual(parts[0], parts[1]);
    }

    [TestMethod]
    public void Write_SaldoColumn_IsBlank()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 1), "SHOP", 10.00m, IsDebit: true);
        var csv = _writer.Write(Statement(tx));
        var parts = csv.Split("\r\n")[1].Split(';');
        // columns: Fecha ctble, Fecha valor, Concepto, Importe, Moneda, Saldo, Moneda, Concepto ampliado
        Assert.AreEqual("", parts[5]);
    }

    [TestMethod]
    public void Write_CurrencyColumns_ArEur()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 1), "SHOP", 10.00m, IsDebit: true);
        var csv = _writer.Write(Statement(tx));
        var parts = csv.Split("\r\n")[1].Split(';');
        Assert.AreEqual("EUR", parts[4]);
        Assert.AreEqual("EUR", parts[6]);
    }

    [TestMethod]
    public void Write_ConceptoAmpliado_IsBlank()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 1), "SHOP", 10.00m, IsDebit: true);
        var csv = _writer.Write(Statement(tx));
        var parts = csv.Split("\r\n")[1].Split(';');
        Assert.AreEqual("", parts[7]);
    }

    [TestMethod]
    public void Write_ThousandsSeparatorAmount_FormattedAsSpanish()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 1), "AIRLINE", 1234.56m, IsDebit: true);
        var csv = _writer.Write(Statement(tx));
        var parts = csv.Split("\r\n")[1].Split(';');
        Assert.AreEqual("-1234,56", parts[3]);
    }

    [TestMethod]
    public void Write_EmptyStatement_OnlyHeader()
    {
        var csv = _writer.Write(Statement());
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.HasCount(1, lines);
    }

    [TestMethod]
    public void Write_DateFormat_DdMmYyyy()
    {
        var tx = new CardTransaction(new DateOnly(2025, 3, 5), "SHOP", 5.00m, IsDebit: true);
        var csv = _writer.Write(Statement(tx));
        var parts = csv.Split("\r\n")[1].Split(';');
        Assert.AreEqual("05-03-2025", parts[0]);
    }
}
