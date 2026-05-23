using FireflyIiiCustomUploader.Core.FireflyIii.Models;
using FireflyIiiCustomUploader.Core.Sync;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class BestGuessAccountTests
{
    private static Account MakeAccount(string id, string name) =>
        new(id, new AccountAttributes(name, "asset", null));

    [TestMethod]
    public void Match_NullHint_ReturnsNull()
    {
        var accounts = new[] { MakeAccount("1", "Advanzia credit card") };

        var result = BestGuessAccount.Match(accounts, null);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Match_NoMatchingAccount_ReturnsNull()
    {
        var accounts = new[] { MakeAccount("1", "Savings account") };

        var result = BestGuessAccount.Match(accounts, "advanzia.*credit");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Match_MatchingAccount_ReturnsIt()
    {
        var account = MakeAccount("42", "Advanzia credit card");
        var accounts = new[] { account };

        var result = BestGuessAccount.Match(accounts, "advanzia.*credit");

        Assert.AreSame(account, result);
    }

    [TestMethod]
    public void Match_CaseInsensitive_Matches()
    {
        var account = MakeAccount("1", "ADVANZIA CREDIT CARD");

        var result = BestGuessAccount.Match([account], "advanzia.*credit");

        Assert.AreSame(account, result);
    }

    [TestMethod]
    public void Match_MultipleAccounts_ReturnsFirst()
    {
        var first = MakeAccount("1", "Advanzia credit card");
        var second = MakeAccount("2", "Advanzia credit card business");
        var accounts = new[] { first, second };

        var result = BestGuessAccount.Match(accounts, "advanzia.*credit");

        Assert.AreSame(first, result);
    }

    [TestMethod]
    public void Match_EmptyAccounts_ReturnsNull()
    {
        var result = BestGuessAccount.Match([], "advanzia.*credit");

        Assert.IsNull(result);
    }
}
