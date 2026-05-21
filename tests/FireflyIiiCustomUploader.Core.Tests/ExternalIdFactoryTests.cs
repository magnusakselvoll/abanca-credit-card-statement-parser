using FireflyIiiCustomUploader.Core.Models;
using FireflyIiiCustomUploader.Core.Sync;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class ExternalIdFactoryTests
{
    [TestMethod]
    public void Create_SameTransaction_SameId()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 15), "GROCERY STORE", 42.50m, IsDebit: true);

        var id1 = ExternalIdFactory.Create("abanca-visa", tx);
        var id2 = ExternalIdFactory.Create("abanca-visa", tx);

        Assert.AreEqual(id1, id2);
    }

    [TestMethod]
    public void Create_StartsWithFormatIdPrefix()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP", 10.00m, IsDebit: true);

        var id = ExternalIdFactory.Create("abanca-visa", tx);

        Assert.IsTrue(id.StartsWith("abanca-visa:"), $"Expected prefix 'abanca-visa:' but got: {id}");
    }

    [TestMethod]
    public void Create_DifferentDates_DifferentIds()
    {
        var tx1 = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP", 10.00m, IsDebit: true);
        var tx2 = new CardTransaction(new DateOnly(2025, 4, 16), "SHOP", 10.00m, IsDebit: true);

        Assert.AreNotEqual(ExternalIdFactory.Create("x", tx1), ExternalIdFactory.Create("x", tx2));
    }

    [TestMethod]
    public void Create_DifferentAmounts_DifferentIds()
    {
        var tx1 = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP", 10.00m, IsDebit: true);
        var tx2 = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP", 11.00m, IsDebit: true);

        Assert.AreNotEqual(ExternalIdFactory.Create("x", tx1), ExternalIdFactory.Create("x", tx2));
    }

    [TestMethod]
    public void Create_DifferentDirections_DifferentIds()
    {
        var tx1 = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP", 10.00m, IsDebit: true);
        var tx2 = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP", 10.00m, IsDebit: false);

        Assert.AreNotEqual(ExternalIdFactory.Create("x", tx1), ExternalIdFactory.Create("x", tx2));
    }

    [TestMethod]
    public void Create_DifferentDescriptions_DifferentIds()
    {
        var tx1 = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP A", 10.00m, IsDebit: true);
        var tx2 = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP B", 10.00m, IsDebit: true);

        Assert.AreNotEqual(ExternalIdFactory.Create("x", tx1), ExternalIdFactory.Create("x", tx2));
    }

    [TestMethod]
    public void Create_NormalizesWhitespace_SameId()
    {
        // Extra spaces should not produce a different ID.
        var tx1 = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP  A", 10.00m, IsDebit: true);
        var tx2 = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP A", 10.00m, IsDebit: true);

        Assert.AreEqual(ExternalIdFactory.Create("x", tx1), ExternalIdFactory.Create("x", tx2));
    }

    [TestMethod]
    public void Create_NormalizesCase_SameId()
    {
        // Lower-case vs upper-case description should produce the same ID.
        var tx1 = new CardTransaction(new DateOnly(2025, 4, 15), "grocery store", 10.00m, IsDebit: true);
        var tx2 = new CardTransaction(new DateOnly(2025, 4, 15), "GROCERY STORE", 10.00m, IsDebit: true);

        Assert.AreEqual(ExternalIdFactory.Create("x", tx1), ExternalIdFactory.Create("x", tx2));
    }

    [TestMethod]
    public void Create_DifferentFormats_DifferentIds()
    {
        var tx = new CardTransaction(new DateOnly(2025, 4, 15), "SHOP", 10.00m, IsDebit: true);

        Assert.AreNotEqual(ExternalIdFactory.Create("format-a", tx), ExternalIdFactory.Create("format-b", tx));
    }
}
