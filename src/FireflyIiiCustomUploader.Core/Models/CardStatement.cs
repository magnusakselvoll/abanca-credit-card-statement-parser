namespace FireflyIiiCustomUploader.Core.Models;

public record CardStatement(
    IReadOnlyList<CardTransaction> Transactions,
    decimal? StatedTotal);
