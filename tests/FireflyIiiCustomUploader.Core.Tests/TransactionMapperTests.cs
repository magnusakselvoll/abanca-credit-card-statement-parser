using FireflyIiiCustomUploader.Core.Models;
using FireflyIiiCustomUploader.Core.Sync;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class TransactionMapperTests
{
    private static readonly CardTransaction Debit =
        new(new DateOnly(2025, 4, 15), "GROCERY STORE", 42.50m, IsDebit: true);

    private static readonly CardTransaction Credit =
        new(new DateOnly(2025, 4, 20), "HOTEL REFUND", 80.00m, IsDebit: false);

    [TestMethod]
    public void ToTransactionSplit_Debit_TypeIsWithdrawal()
    {
        var split = TransactionMapper.ToTransactionSplit(Debit, "ext-1", "My Card", "run-tag");
        Assert.AreEqual("withdrawal", split.Type);
    }

    [TestMethod]
    public void ToTransactionSplit_Credit_TypeIsDeposit()
    {
        var split = TransactionMapper.ToTransactionSplit(Credit, "ext-2", "My Card", "run-tag");
        Assert.AreEqual("deposit", split.Type);
    }

    [TestMethod]
    public void ToTransactionSplit_Debit_SourceNameIsAccount()
    {
        var split = TransactionMapper.ToTransactionSplit(Debit, "ext-1", "My Card", "run-tag");
        Assert.AreEqual("My Card", split.SourceName);
        Assert.IsNull(split.DestinationName);
    }

    [TestMethod]
    public void ToTransactionSplit_Credit_DestinationNameIsAccount()
    {
        var split = TransactionMapper.ToTransactionSplit(Credit, "ext-2", "My Card", "run-tag");
        Assert.AreEqual("My Card", split.DestinationName);
        Assert.IsNull(split.SourceName);
    }

    [TestMethod]
    public void ToTransactionSplit_Amount_FormattedAsTwoDecimals()
    {
        var split = TransactionMapper.ToTransactionSplit(Debit, "ext-1", "My Card", "run-tag");
        Assert.AreEqual("42.50", split.Amount);
    }

    [TestMethod]
    public void ToTransactionSplit_CurrencyCode_IsEur()
    {
        var split = TransactionMapper.ToTransactionSplit(Debit, "ext-1", "My Card", "run-tag");
        Assert.AreEqual("EUR", split.CurrencyCode);
    }

    [TestMethod]
    public void ToTransactionSplit_ExternalId_PassedThrough()
    {
        var split = TransactionMapper.ToTransactionSplit(Debit, "my-ext-id", "My Card", "run-tag");
        Assert.AreEqual("my-ext-id", split.ExternalId);
    }

    [TestMethod]
    public void ToTransactionSplit_Tags_ContainRunTag()
    {
        var split = TransactionMapper.ToTransactionSplit(Debit, "ext-1", "My Card", "ffcu-upload-2025-04-15");
        Assert.IsNotNull(split.Tags);
        Assert.IsTrue(split.Tags.Contains("ffcu-upload-2025-04-15"));
    }

    [TestMethod]
    public void ToTransactionSplit_Description_MatchesTransaction()
    {
        var split = TransactionMapper.ToTransactionSplit(Debit, "ext-1", "My Card", "run-tag");
        Assert.AreEqual("GROCERY STORE", split.Description);
    }

    [TestMethod]
    public void ToTransactionSplit_Date_MatchesTransaction()
    {
        var split = TransactionMapper.ToTransactionSplit(Debit, "ext-1", "My Card", "run-tag");
        Assert.AreEqual(new DateOnly(2025, 4, 15), split.Date);
    }

    [TestMethod]
    public void ToTransactionSplit_WithCategory_NotesContainsLabel()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 15), "ACME STORE", 42.50m, IsDebit: true, Category: "groceries");
        var split = TransactionMapper.ToTransactionSplit(tx, "ext-1", "My Card", "run-tag");
        Assert.AreEqual("Category: groceries", split.Notes);
    }

    [TestMethod]
    public void ToTransactionSplit_WithoutCategory_NotesIsNull()
    {
        var split = TransactionMapper.ToTransactionSplit(Debit, "ext-1", "My Card", "run-tag");
        Assert.IsNull(split.Notes);
    }
}
