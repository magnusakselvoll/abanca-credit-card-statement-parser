using System.Globalization;
using System.Text.RegularExpressions;
using FireflyIiiCustomUploader.Core.Models;

namespace FireflyIiiCustomUploader.Core.Parsing.Advanzia;

public class AdvanziaStatementParser : IStatementParser
{
    public string FormatId => "advanzia";

    // Matches any 4-digit number used to derive the century prefix from the export header.
    private static readonly Regex FourDigitYearRegex =
        new(@"\b(\d{4})\b", RegexOptions.Compiled);

    // Matches a transaction row: dd.mm.yy  description  category  €  [-]amount
    // Group 1 = date (dd.mm.yy), Group 2 = description (non-greedy, merchant text),
    // Group 3 = category (single token before €), Group 4 = optional minus sign,
    // Group 5 = amount magnitude (European format: 1.234,56).
    private static readonly Regex TransactionRegex =
        new(@"^(\d{2}\.\d{2}\.\d{2})\s+(.+?)\s+(\S+)\s+€\s*(-?)([\d.]+,\d{2})\s*$",
            RegexOptions.Compiled);

    public bool CanParse(IReadOnlyList<string> lines) =>
        lines.Any(l => l.Contains("Counterparty", StringComparison.OrdinalIgnoreCase) &&
                       l.Contains("Category", StringComparison.OrdinalIgnoreCase));

    public CardStatement Parse(IReadOnlyList<string> lines)
    {
        int centuryPrefix = FindCenturyPrefix(lines);
        var transactions = new List<CardTransaction>();

        foreach (var line in lines)
        {
            var match = TransactionRegex.Match(line);
            if (!match.Success)
                continue;

            var dateStr = match.Groups[1].Value;
            var description = match.Groups[2].Value.Trim();
            var category = match.Groups[3].Value.Trim();
            var minusSign = match.Groups[4].Value;
            var amountStr = match.Groups[5].Value;

            var date = ExpandTransactionDate(dateStr, centuryPrefix);
            var amount = ParseEuropeanDecimal(amountStr);
            var isDebit = minusSign == "-";

            transactions.Add(new CardTransaction(date, description, amount, isDebit, category));
        }

        return new CardStatement(transactions, null);
    }

    private static int FindCenturyPrefix(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = FourDigitYearRegex.Match(line);
            if (match.Success)
                return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) / 100;
        }
        return 20;
    }

    private static DateOnly ExpandTransactionDate(string ddMmYy, int centuryPrefix)
    {
        var parts = ddMmYy.Split('.');
        int day = int.Parse(parts[0], CultureInfo.InvariantCulture);
        int month = int.Parse(parts[1], CultureInfo.InvariantCulture);
        int year = centuryPrefix * 100 + int.Parse(parts[2], CultureInfo.InvariantCulture);
        return new DateOnly(year, month, day);
    }

    private static decimal ParseEuropeanDecimal(string value)
    {
        var normalized = value.Replace(".", "").Replace(",", ".");
        return decimal.Parse(normalized, CultureInfo.InvariantCulture);
    }
}
