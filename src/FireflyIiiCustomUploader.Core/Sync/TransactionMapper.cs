using System.Globalization;
using FireflyIiiCustomUploader.Core.FireflyIii.Models;
using FireflyIiiCustomUploader.Core.Models;

namespace FireflyIiiCustomUploader.Core.Sync;

public static class TransactionMapper
{
    public static TransactionSplit ToTransactionSplit(
        CardTransaction tx,
        string externalId,
        string assetAccountName,
        string runTag)
    {
        // For a credit card:
        //   Debit (D)  = money spent = withdrawal from the card account
        //   Credit (H) = refund/payment = deposit to the card account
        var type = tx.IsDebit ? "withdrawal" : "deposit";
        var amount = tx.Amount.ToString("0.00", CultureInfo.InvariantCulture);

        return new TransactionSplit(
            Type: type,
            Date: tx.Date,
            Amount: amount,
            Description: tx.Description,
            CurrencyCode: "EUR",
            ExternalId: externalId,
            SourceName: tx.IsDebit ? assetAccountName : null,
            DestinationName: tx.IsDebit ? null : assetAccountName,
            Tags: [runTag],
            Notes: null);
    }
}
