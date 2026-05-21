using FireflyIiiCustomUploader.Core.FireflyIii;
using FireflyIiiCustomUploader.Core.FireflyIii.Models;
using FireflyIiiCustomUploader.Core.Models;
using FireflyIiiCustomUploader.Core.Sync;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class UploadPlannerTests
{
    [TestMethod]
    public async Task BuildPlanAsync_AllTransactionsSameDate_WidensQueryEnd()
    {
        var date = new DateOnly(2026, 1, 22);
        var statement = new CardStatement(
        [
            new CardTransaction(date, "Shop A", 10.00m, true),
            new CardTransaction(date, "Shop B", 5.50m, true),
        ], null);

        var fake = new FakeFireflyClient();
        var sut = new UploadPlanner(fake, NullLogger<UploadPlanner>.Instance);

        var plan = await sut.BuildPlanAsync(statement, "advanzia", "16", "My Card");

        Assert.AreEqual(date, fake.CapturedDateFrom);
        Assert.AreEqual(date.AddDays(1), fake.CapturedDateTo);
        Assert.HasCount(2, plan.Items);
        Assert.IsTrue(plan.Items.All(i => i.Decision == UploadDecision.Create));
    }

    [TestMethod]
    public async Task BuildPlanAsync_MultiDayRange_PassesRangeUnchanged()
    {
        var dateA = new DateOnly(2026, 1, 22);
        var dateB = new DateOnly(2026, 1, 25);
        var statement = new CardStatement(
        [
            new CardTransaction(dateA, "Shop A", 10.00m, true),
            new CardTransaction(dateB, "Shop B", 5.50m, true),
        ], null);

        var fake = new FakeFireflyClient();
        var sut = new UploadPlanner(fake, NullLogger<UploadPlanner>.Instance);

        await sut.BuildPlanAsync(statement, "advanzia", "16", "My Card");

        Assert.AreEqual(dateA, fake.CapturedDateFrom);
        Assert.AreEqual(dateB, fake.CapturedDateTo);
    }

    private sealed class FakeFireflyClient : IFireflyIiiClient
    {
        public DateOnly CapturedDateFrom { get; private set; }
        public DateOnly CapturedDateTo { get; private set; }

        public Task<IReadOnlyList<Account>> GetAssetAccountsAsync(CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(
            string accountId, DateOnly dateFrom, DateOnly dateTo, CancellationToken cancellationToken = default)
        {
            CapturedDateFrom = dateFrom;
            CapturedDateTo = dateTo;
            return Task.FromResult<IReadOnlyList<Transaction>>([]);
        }

        public Task CreateTransactionAsync(TransactionStore transaction, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}
