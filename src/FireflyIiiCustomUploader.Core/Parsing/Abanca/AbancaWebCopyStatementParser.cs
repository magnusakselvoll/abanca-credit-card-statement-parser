using System.Globalization;
using System.Text.RegularExpressions;
using FireflyIiiCustomUploader.Core.Models;

namespace FireflyIiiCustomUploader.Core.Parsing.Abanca;

public class AbancaWebCopyStatementParser : IStatementParser
{
    public string FormatId => "abanca-web-copy";
    public string DisplayName => "Abanca credit card web copy";
    public string? AccountNameHint => "abanca.*credit";

    // Matches amounts like "119,07 EUR" or "-7,84 EUR"
    private static readonly Regex AmountRegex =
        new(@"^(-?)([\d.]+,\d{2})\s*EUR$", RegexOptions.Compiled);

    public bool CanParse(IReadOnlyList<string> lines) =>
        lines.Any(l => TryParseRow(l, out _));

    public CardStatement Parse(IReadOnlyList<string> lines)
    {
        var transactions = new List<CardTransaction>();
        foreach (var line in lines)
        {
            if (TryParseRow(line, out var tx))
                transactions.Add(tx);
        }
        return new CardStatement(transactions, null);
    }

    // Columns (tab-separated): [0]=TIT. [1]=date [2]=tipo [3]=situación [4]=concepto [5]=importe
    private static bool TryParseRow(string line, out CardTransaction transaction)
    {
        transaction = default!;
        var parts = line.Split('\t');
        if (parts.Length < 6)
            return false;

        if (!DateOnly.TryParseExact(parts[1].Trim(), "dd/MM/yyyy",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return false;

        var amountMatch = AmountRegex.Match(parts[5].Trim());
        if (!amountMatch.Success)
            return false;

        var isDebit = amountMatch.Groups[1].Value == "-";
        var amount = AbancaStatementParser.ParseSpanishDecimal(amountMatch.Groups[2].Value);
        var category = parts[2].Trim();
        var description = parts[4].Trim();

        transaction = new CardTransaction(date, description, amount, isDebit,
            string.IsNullOrEmpty(category) ? null : category);
        return true;
    }
}
