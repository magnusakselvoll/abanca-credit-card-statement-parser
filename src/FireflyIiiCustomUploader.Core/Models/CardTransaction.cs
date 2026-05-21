namespace FireflyIiiCustomUploader.Core.Models;

public record CardTransaction(
    DateOnly Date,
    string Description,
    decimal Amount,
    bool IsDebit);
