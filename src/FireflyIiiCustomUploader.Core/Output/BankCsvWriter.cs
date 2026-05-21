using System.Globalization;
using System.Text;
using FireflyIiiCustomUploader.Core.Models;

namespace FireflyIiiCustomUploader.Core.Output;

public class BankCsvWriter
{
    private const string Header = "Fecha ctble;Fecha valor;Concepto;Importe;Moneda;Saldo;Moneda;Concepto ampliado";

    public string Write(CardStatement statement)
    {
        var sb = new StringBuilder();
        sb.Append(Header);
        sb.Append("\r\n");

        foreach (var tx in statement.Transactions)
        {
            var dateStr = tx.Date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
            var importe = FormatImporte(tx.Amount, tx.IsDebit);
            sb.Append(dateStr);
            sb.Append(';');
            sb.Append(dateStr);
            sb.Append(';');
            sb.Append(tx.Description);
            sb.Append(';');
            sb.Append(importe);
            sb.Append(';');
            sb.Append("EUR;;EUR;");
            sb.Append("\r\n");
        }

        return sb.ToString();
    }

    private static string FormatImporte(decimal amount, bool isDebit)
    {
        var abs = amount.ToString("0.00", CultureInfo.InvariantCulture).Replace(".", ",");
        return isDebit ? $"-{abs}" : abs;
    }
}
