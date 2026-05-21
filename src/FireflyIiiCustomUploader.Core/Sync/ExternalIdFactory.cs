using System.Security.Cryptography;
using System.Text;
using FireflyIiiCustomUploader.Core.Models;

namespace FireflyIiiCustomUploader.Core.Sync;

public static class ExternalIdFactory
{
    public static string Create(string formatId, CardTransaction transaction)
    {
        var normalized = NormalizeDescription(transaction.Description);
        var amountCents = (long)(transaction.Amount * 100);
        var direction = transaction.IsDebit ? "D" : "H";
        var raw = $"{transaction.Date:yyyy-MM-dd}|{amountCents}|{direction}|{normalized}";

        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        return $"{formatId}:{hash}";
    }

    public static string ContentKey(CardTransaction transaction)
    {
        var amountCents = (long)(transaction.Amount * 100);
        return $"{transaction.Date:yyyy-MM-dd}|{amountCents}|{NormalizeDescription(transaction.Description)}";
    }

    internal static string NormalizeDescription(string description) =>
        string.Join(" ", description.Trim().Split((char[])null!, StringSplitOptions.RemoveEmptyEntries))
              .ToUpperInvariant();
}
