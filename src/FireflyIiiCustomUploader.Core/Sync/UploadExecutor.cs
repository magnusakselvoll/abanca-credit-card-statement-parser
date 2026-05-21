using FireflyIiiCustomUploader.Core.FireflyIii;
using FireflyIiiCustomUploader.Core.FireflyIii.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FireflyIiiCustomUploader.Core.Options;

namespace FireflyIiiCustomUploader.Core.Sync;

public record UploadResult(
    int Created,
    int SkippedDuplicate,
    int SkippedExcluded,
    int Errors,
    string RunTag);

public class UploadExecutor
{
    private readonly IFireflyIiiClient _fireflyClient;
    private readonly ILogger<UploadExecutor> _logger;
    private readonly FireflyIiiCustomUploaderOptions _options;

    public UploadExecutor(
        IFireflyIiiClient fireflyClient,
        ILogger<UploadExecutor> logger,
        IOptions<FireflyIiiCustomUploaderOptions> options)
    {
        _fireflyClient = fireflyClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<UploadResult> ExecuteAsync(
        UploadPlan plan,
        IReadOnlySet<int> includedIndices,
        CancellationToken cancellationToken = default)
    {
        var runTag = $"{_options.RunTagPrefix}-{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}";
        int created = 0, skippedDuplicate = 0, skippedExcluded = 0, errors = 0;

        for (int i = 0; i < plan.Items.Count; i++)
        {
            var item = plan.Items[i];

            if (item.Decision == UploadDecision.SkipDuplicate)
            {
                skippedDuplicate++;
                continue;
            }
            if (!includedIndices.Contains(i))
            {
                skippedExcluded++;
                continue;
            }

            var split = TransactionMapper.ToTransactionSplit(
                item.Transaction, item.ExternalId, plan.AssetAccountName, runTag);
            var store = new TransactionStore(ErrorIfDuplicateHash: false, [split]);

            try
            {
                await _fireflyClient.CreateTransactionAsync(store, cancellationToken);
                created++;
            }
            catch (Exception ex)
            {
                errors++;
                _logger.LogError(ex, "Failed to create transaction {ExternalId}.", item.ExternalId);
            }
        }

        _logger.LogInformation(
            "Upload complete. Created: {Created}, Duplicates: {Dup}, Excluded: {Excl}, Errors: {Err}. Run tag: {Tag}",
            created, skippedDuplicate, skippedExcluded, errors, runTag);

        return new UploadResult(created, skippedDuplicate, skippedExcluded, errors, runTag);
    }
}
