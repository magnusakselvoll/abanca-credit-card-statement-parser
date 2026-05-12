using System.Globalization;
using System.Text.RegularExpressions;
using AbancaCardParser.Core.Models;

namespace AbancaCardParser.Core.Parsing;

public class StatementTextParser
{
    // Matches a 4-digit year inside a dd-mm-yyyy date, used to derive the century prefix.
    private static readonly Regex FourDigitYearRegex =
        new(@"\b\d{2}-\d{2}-(\d{4})\b", RegexOptions.Compiled);

    // Matches a transaction row: [optional non-digit junk] dd-mm-yy  cardCode  description  amount  D/H
    // The leading [^0-9]* handles side-label characters (e.g. "K" from "Mod. KQ0") that PdfPig
    // places on the same Y coordinate as the first transaction row.
    // Amount uses Spanish format: optional thousands dot, mandatory comma-decimal (e.g. 1.234,56).
    private static readonly Regex TransactionRegex =
        new(@"^[^0-9]*(\d{2}-\d{2}-\d{2})\s+(\d{2})\s+(.+?)\s+([\d\.]+,\d{2})\s+([DH])\s*$",
            RegexOptions.Compiled);

    // Extracts the trailing amount from the TOTAL OPERACIONES TARJETA line.
    private static readonly Regex TotalAmountRegex =
        new(@"([\d\.]+,\d{2})\s*$", RegexOptions.Compiled);

    public CardStatement Parse(IReadOnlyList<string> lines)
    {
        int centuryPrefix = FindCenturyPrefix(lines);
        var transactions = new List<CardTransaction>();
        decimal? statedTotal = null;

        foreach (var line in lines)
        {
            if (line.Contains("TOTAL OPERACIONES TARJETA", StringComparison.OrdinalIgnoreCase))
            {
                var totalMatch = TotalAmountRegex.Match(line);
                if (totalMatch.Success)
                    statedTotal = ParseSpanishDecimal(totalMatch.Groups[1].Value);
                break;
            }

            var match = TransactionRegex.Match(line);
            if (!match.Success)
                continue;

            var dateStr = match.Groups[1].Value;
            var description = match.Groups[3].Value.Trim();
            var amountStr = match.Groups[4].Value;
            var sign = match.Groups[5].Value;

            var date = ExpandTransactionDate(dateStr, centuryPrefix);
            var amount = ParseSpanishDecimal(amountStr);
            var isDebit = sign == "D";

            transactions.Add(new CardTransaction(date, description, amount, isDebit));
        }

        return new CardStatement(transactions, statedTotal);
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
        var parts = ddMmYy.Split('-');
        int day = int.Parse(parts[0], CultureInfo.InvariantCulture);
        int month = int.Parse(parts[1], CultureInfo.InvariantCulture);
        int year = centuryPrefix * 100 + int.Parse(parts[2], CultureInfo.InvariantCulture);
        return new DateOnly(year, month, day);
    }

    public static decimal ParseSpanishDecimal(string value)
    {
        var normalized = value.Replace(".", "").Replace(",", ".");
        return decimal.Parse(normalized, CultureInfo.InvariantCulture);
    }
}
