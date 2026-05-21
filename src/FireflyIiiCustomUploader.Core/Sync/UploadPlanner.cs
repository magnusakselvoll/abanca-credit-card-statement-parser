using FireflyIiiCustomUploader.Core.FireflyIii;
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

        var existing = await _fireflyClient.GetTransactionsAsync(
            assetAccountId, dateFrom, dateTo, cancellationToken);

        var existingExternalIds = existing
            .SelectMany(t => t.Attributes.Transactions)
            .Where(s => s.ExternalId is not null)
            .Select(s => s.ExternalId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation(
            "Building upload plan for {Count} transactions ({From} – {To}). Found {Existing} existing in Firefly III.",
            statement.Transactions.Count, dateFrom, dateTo, existingExternalIds.Count);

        var items = new List<UploadPlanItem>();
        foreach (var tx in statement.Transactions)
        {
            var externalId = ExternalIdFactory.Create(formatId, tx);

            UploadDecision decision;
            if (IsAmortizacion(tx))
                decision = UploadDecision.SkipAmortization;
            else if (existingExternalIds.Contains(externalId))
                decision = UploadDecision.SkipDuplicate;
            else
                decision = UploadDecision.Create;

            items.Add(new UploadPlanItem(tx, decision, externalId));
        }

        return new UploadPlan(formatId, assetAccountId, assetAccountName, items);
    }

    private static bool IsAmortizacion(CardTransaction tx) =>
        tx.Description.StartsWith("AMORTIZACION DEUDA", StringComparison.OrdinalIgnoreCase);
}
