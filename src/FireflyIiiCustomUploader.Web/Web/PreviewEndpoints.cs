using FireflyIiiCustomUploader.Core.Sync;

namespace FireflyIiiCustomUploader.Web.Web;

internal static class PreviewEndpoints
{
    internal static void Map(WebApplication app)
    {
        app.MapGet("/preview/{token}", GetPreview);
        app.MapPost("/submit", PostSubmit);
    }

    private static IResult GetPreview(string token, ReviewState reviewState)
    {
        var plan = reviewState.GetIfValid(token);
        if (plan is null)
            return Results.Content(
                Html.Error("Preview expired or not found. Please upload the file again."),
                "text/html");

        return Results.Content(Html.Preview(plan, token), "text/html");
    }

    private static async Task<IResult> PostSubmit(
        HttpRequest request,
        ReviewState reviewState,
        UploadExecutor executor,
        CancellationToken cancellationToken)
    {
        var form = await request.ReadFormAsync(cancellationToken);
        var token = form["token"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
            return Results.Content(Html.Error("Missing submission token."), "text/html");

        var plan = reviewState.TakeIfValid(token);
        if (plan is null)
            return Results.Content(
                Html.Error("Preview expired or not found. Please upload the file again."),
                "text/html");

        var includedIndices = form["include"]
            .Where(v => int.TryParse(v, out _))
            .Select(v => int.Parse(v!))
            .ToHashSet();

        // Re-validate: only allow indices whose plan decision permits import.
        var allowedIndices = includedIndices
            .Where(i => i >= 0 && i < plan.Items.Count &&
                        plan.Items[i].Decision is UploadDecision.Create or UploadDecision.SkipDuplicate)
            .ToHashSet();

        UploadResult result;
        try
        {
            result = await executor.ExecuteAsync(plan, allowedIndices, cancellationToken);
        }
        catch (Exception ex)
        {
            return Results.Content(Html.Error($"Upload failed: {ex.Message}"), "text/html");
        }

        return Results.Content(Html.Result(result), "text/html");
    }
}
