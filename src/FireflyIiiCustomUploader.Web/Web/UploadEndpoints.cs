using FireflyIiiCustomUploader.Core.FireflyIii;
using FireflyIiiCustomUploader.Core.Output;
using FireflyIiiCustomUploader.Core.Parsing;
using FireflyIiiCustomUploader.Core.Sync;
using Microsoft.AspNetCore.Mvc;

namespace FireflyIiiCustomUploader.Web.Web;

internal static class UploadEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/", GetUploadForm);
        app.MapPost("/upload", PostUpload).DisableAntiforgery();
        app.MapGet("/select/{token}", GetSelect);
        app.MapPost("/select", PostSelect).DisableAntiforgery();
        app.MapGet("/download-csv/{token}", GetDownloadCsv);
    }

    private static IResult GetUploadForm() =>
        Results.Content(Html.UploadForm(null), "text/html");

    private static async Task<IResult> PostUpload(
        IFormFile? file,
        IPdfTextExtractor textExtractor,
        StatementParserRegistry parserRegistry,
        PendingUploadStore pendingStore,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.Content(Html.UploadForm("No file uploaded."), "text/html");

        IReadOnlyList<string> lines;
        try
        {
            using var stream = file.OpenReadStream();
            lines = textExtractor.ExtractLines(stream);
        }
        catch (Exception ex)
        {
            return Results.Content(Html.UploadForm($"Could not read PDF: {ex.Message}"), "text/html");
        }

        var detected = parserRegistry.FindParser(lines);
        var token = pendingStore.Add(new PendingUpload(lines, detected?.FormatId));
        return Results.Redirect($"/select/{token}");
    }

    private static async Task<IResult> GetSelect(
        string token,
        PendingUploadStore pendingStore,
        StatementParserRegistry parserRegistry,
        IFireflyIiiClient fireflyClient,
        CancellationToken cancellationToken)
    {
        var pending = pendingStore.GetIfValid(token);
        if (pending is null)
            return Results.Content(
                Html.Error("Upload session expired or not found. Please upload the file again."),
                "text/html");

        IReadOnlyList<Core.FireflyIii.Models.Account> accounts;
        try
        {
            accounts = (await fireflyClient.GetAssetAccountsAsync(cancellationToken))
                .OrderBy(a => a.Attributes.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            return Results.Content(
                Html.Error($"Could not reach Firefly III: {ex.Message}"),
                "text/html");
        }

        var bestGuessParser = pending.DetectedFormatId is not null
            ? parserRegistry.GetParser(pending.DetectedFormatId)
            : null;
        var selectedAccountId = BestGuessAccount.Match(accounts, bestGuessParser?.AccountNameHint)?.Id;

        return Results.Content(
            Html.SelectForm(token, parserRegistry.Parsers, pending.DetectedFormatId, accounts, selectedAccountId, null),
            "text/html");
    }

    private static async Task<IResult> PostSelect(
        HttpRequest request,
        PendingUploadStore pendingStore,
        StatementParserRegistry parserRegistry,
        IFireflyIiiClient fireflyClient,
        UploadPlanner planner,
        ReviewState reviewState,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var token = form["token"].FirstOrDefault();
        var formatId = form["formatId"].FirstOrDefault();
        var accountId = form["accountId"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
            return Results.Content(Html.Error("Missing session token."), "text/html");

        var pending = pendingStore.GetIfValid(token);
        if (pending is null)
            return Results.Content(
                Html.Error("Upload session expired or not found. Please upload the file again."),
                "text/html");

        // Fetch accounts (needed for re-rendering on error and for resolving the chosen account).
        IReadOnlyList<Core.FireflyIii.Models.Account> accounts;
        try
        {
            accounts = (await fireflyClient.GetAssetAccountsAsync(cancellationToken))
                .OrderBy(a => a.Attributes.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            return Results.Content(
                Html.Error($"Could not reach Firefly III: {ex.Message}"),
                "text/html");
        }

        IResult RenderSelectWithError(string error) =>
            Results.Content(
                Html.SelectForm(token, parserRegistry.Parsers, formatId, accounts, accountId, error),
                "text/html");

        if (string.IsNullOrWhiteSpace(formatId))
            return RenderSelectWithError("Please select a statement format.");

        var parser = parserRegistry.GetParser(formatId);
        if (parser is null)
            return RenderSelectWithError("Unknown format selected. Please choose from the list.");

        if (string.IsNullOrWhiteSpace(accountId))
            return RenderSelectWithError("Please select an account.");

        var account = accounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null)
            return RenderSelectWithError("Selected account not found in Firefly III.");

        Core.Models.CardStatement statement;
        try
        {
            statement = parser.Parse(pending.Lines);
        }
        catch (Exception ex)
        {
            return RenderSelectWithError(
                $"Could not parse file as \"{parser.DisplayName}\": {ex.Message}. Try a different format.");
        }

        if (statement.Transactions.Count == 0)
            return RenderSelectWithError(
                $"No transactions found when parsing as \"{parser.DisplayName}\". Try a different format.");

        UploadPlan plan;
        try
        {
            plan = await planner.BuildPlanAsync(
                statement, parser.FormatId, account.Id, account.Attributes.Name, cancellationToken);
        }
        catch (Exception ex)
        {
            return Results.Content(
                Html.Error($"Could not check Firefly III for duplicates: {ex.Message}"),
                "text/html");
        }

        var planToken = reviewState.Add(plan);
        return Results.Redirect($"/preview/{planToken}");
    }

    private static IResult GetDownloadCsv(
        string token,
        ReviewState reviewState)
    {
        var plan = reviewState.GetIfValid(token);
        if (plan is null)
            return Results.Content(Html.Error("Preview expired or not found. Please upload the file again."), "text/html");

        var writer = new BankCsvWriter();
        var transactions = plan.Items
            .Select(i => i.Transaction)
            .ToList();
        var statement = new Core.Models.CardStatement(transactions, null);
        var csv = writer.Write(statement);

        return Results.File(
            System.Text.Encoding.UTF8.GetBytes(csv),
            "text/csv",
            "statement.csv");
    }
}
