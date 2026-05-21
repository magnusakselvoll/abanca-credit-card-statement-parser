using System.Globalization;
using FireflyIiiCustomUploader.Core.FireflyIii;
using FireflyIiiCustomUploader.Core.FireflyIii.Models;
using FireflyIiiCustomUploader.Core.Models;
using Microsoft.Extensions.Logging;

namespace FireflyIiiCustomUploader.Core.Sync;

public class UploadPlanner
{
    private readonly IFireflyIiiClient _fireflyClient;
    private readonly ILogger<UploadPlanner> _logger;

    public UploadPlanner(IFireflyIiiClient fireflyClient, ILogger<UploadPlanner> logger)
    {
        _fireflyClient = fireflyClient;
        _logger = logger;
    }

    public async Task<UploadPlan> BuildPlanAsync(
        CardStatement statement,
        string formatId,
        string assetAccountId,
        string assetAccountName,
        CancellationToken cancellationToken = default)
    {
        if (statement.Transactions.Count == 0)
            return new UploadPlan(formatId, assetAccountId, assetAccountName, []);

        var dateFrom = statement.Transactions.Min(t => t.Date);
        var dateTo = statement.Transactions.Max(t => t.Date);

        // Firefly III's range filter requires start < end (equal dates -> HTTP 422). A statement
        // whose transactions all fall on one day collapses the range, so widen the end by a day.
        var queryTo = dateTo > dateFrom ? dateTo : dateFrom.AddDays(1);

        var existing = await _fireflyClient.GetTransactionsAsync(
            assetAccountId, dateFrom, queryTo, cancellationToken);

        var allSplits = existing.SelectMany(t => t.Attributes.Transactions).ToList();

        var existingExternalIds = allSplits
            .Where(s => s.ExternalId is not null)
            .Select(s => s.ExternalId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingContentKeys = allSplits
            .Select(ContentKeyFromSplit)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Building upload plan for {Count} transactions ({From} – {To}). Found {Existing} existing in Firefly III.",
            statement.Transactions.Count, dateFrom, dateTo, allSplits.Count);

        var items = new List<UploadPlanItem>();
        foreach (var tx in statement.Transactions)
        {
            var externalId = ExternalIdFactory.Create(formatId, tx);
            var contentKey = ExternalIdFactory.ContentKey(tx);

            var decision = (existingExternalIds.Contains(externalId) || existingContentKeys.Contains(contentKey))
                ? UploadDecision.SkipDuplicate
                : UploadDecision.Create;

            items.Add(new UploadPlanItem(tx, decision, externalId));
        }

        return new UploadPlan(formatId, assetAccountId, assetAccountName, items);
    }

    private static string? ContentKeyFromSplit(TransactionSplitAttributes split)
    {
        if (!decimal.TryParse(split.Amount, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
            return null;
        var amountCents = (long)(amount * 100);
        return $"{split.Date:yyyy-MM-dd}|{amountCents}|{ExternalIdFactory.NormalizeDescription(split.Description ?? "")}";
    }
}
