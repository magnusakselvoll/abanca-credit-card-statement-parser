using System.Text.RegularExpressions;
using FireflyIiiCustomUploader.Core.FireflyIii.Models;

namespace FireflyIiiCustomUploader.Core.Sync;

public static class BestGuessAccount
{
    public static Account? Match(IReadOnlyList<Account> accounts, string? hintRegex)
    {
        if (hintRegex is null)
            return null;

        return accounts.FirstOrDefault(
            a => Regex.IsMatch(a.Attributes.Name, hintRegex, RegexOptions.IgnoreCase));
    }
}
