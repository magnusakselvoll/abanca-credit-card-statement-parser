using FireflyIiiCustomUploader.Core.Models;

namespace FireflyIiiCustomUploader.Core.Sync;

public enum UploadDecision
{
    Create,
    SkipDuplicate,
    SkipAmortization,
}

public record UploadPlanItem(
    CardTransaction Transaction,
    UploadDecision Decision,
    string ExternalId);

public record UploadPlan(
    string FormatId,
    string AssetAccountId,
    string AssetAccountName,
    IReadOnlyList<UploadPlanItem> Items);
