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
        app.MapPost("/upload", PostUpload);
        app.MapGet("/download-csv/{token}", GetDownloadCsv);
    }

    private static async Task<IResult> GetUploadForm(
        IFireflyIiiClient fireflyClient,
        CancellationToken cancellationToken)
    {
        try
        {
            var accounts = await fireflyClient.GetAssetAccountsAsync(cancellationToken);
            return Results.Content(Html.UploadForm(accounts, null), "text/html");
        }
        catch (Exception ex)
        {
            return Results.Content(Html.UploadForm([], $"Could not reach Firefly III: {ex.Message}"), "text/html");
        }
    }

    private static async Task<IResult> PostUpload(
        IFormFile? file,
        [FromForm] string? accountId,
        IPdfTextExtractor textExtractor,
        StatementParserRegistry parserRegistry,
        IFireflyIiiClient fireflyClient,
        UploadPlanner planner,
        ReviewState reviewState,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return Results.Content(Html.Error("No file uploaded."), "text/html");

        if (string.IsNullOrWhiteSpace(accountId))
            return Results.Content(Html.Error("No account selected."), "text/html");

        IReadOnlyList<string> lines;
        try
        {
            using var stream = file.OpenReadStream();
            lines = textExtractor.ExtractLines(stream);
        }
        catch (Exception ex)
        {
            return Results.Content(Html.Error($"Could not read PDF: {ex.Message}"), "text/html");
        }

        var parser = parserRegistry.FindParser(lines);
        if (parser is null)
            return Results.Content(
                Html.Error("Unrecognized statement format. This tool currently supports Abanca VISA credit card statements only."),
                "text/html");

        Core.Models.CardStatement statement;
        try
        {
            statement = parser.Parse(lines);
        }
        catch (Exception ex)
        {
            return Results.Content(Html.Error($"Failed to parse statement: {ex.Message}"), "text/html");
        }

        var accounts = await fireflyClient.GetAssetAccountsAsync(cancellationToken);
        var account = accounts.FirstOrDefault(a => a.Id == accountId);
        if (account is null)
            return Results.Content(Html.Error("Selected account not found in Firefly III."), "text/html");

        UploadPlan plan;
        try
        {
            plan = await planner.BuildPlanAsync(
                statement, parser.FormatId, account.Id, account.Attributes.Name, cancellationToken);
        }
        catch (Exception ex)
        {
            return Results.Content(Html.Error($"Could not check Firefly III for duplicates: {ex.Message}"), "text/html");
        }

        var token = reviewState.Add(plan);
        return Results.Redirect($"/preview/{token}");
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
