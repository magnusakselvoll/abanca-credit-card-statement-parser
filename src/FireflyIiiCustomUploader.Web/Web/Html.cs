using System.Text;
using FireflyIiiCustomUploader.Core.FireflyIii.Models;
using FireflyIiiCustomUploader.Core.Parsing;
using FireflyIiiCustomUploader.Core.Sync;

namespace FireflyIiiCustomUploader.Web.Web;

internal static class Html
{
    // $$""" uses {{expr}} for interpolation so CSS { } are literal.
    private static string Page(string title, string body) => $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <title>{{Encode(title)}} — Firefly III Custom Uploader</title>
          <style>
            body { font-family: system-ui, sans-serif; max-width: 960px; margin: 2rem auto; padding: 0 1rem; color: #222; }
            h1 { font-size: 1.4rem; margin-bottom: 1.5rem; }
            h2 { font-size: 1.1rem; margin-top: 2rem; }
            table { border-collapse: collapse; width: 100%; }
            th, td { text-align: left; padding: .4rem .7rem; border-bottom: 1px solid #ddd; }
            th { background: #f5f5f5; }
            .btn { display: inline-block; padding: .3rem .8rem; border: none; border-radius: 4px; cursor: pointer; font-size: .9rem; text-decoration: none; background: #0d6efd; color: #fff; }
            .btn-secondary { background: #6c757d; }
            .btn-danger { background: #dc3545; }
            .banner { padding: .6rem 1rem; border-radius: 4px; margin-bottom: 1rem; }
            .banner-success { background: #d4edda; color: #155724; }
            .banner-error { background: #f8d7da; color: #721c24; }
            label { font-weight: 600; display: block; margin-bottom: .3rem; }
            input[type=file], select { padding: .4rem .6rem; border: 1px solid #ccc; border-radius: 4px; width: 100%; max-width: 500px; box-sizing: border-box; margin-bottom: 1rem; }
            .field { margin-bottom: 1rem; }
            .actions { margin-top: 1.5rem; display: flex; gap: .5rem; align-items: center; flex-wrap: wrap; }
            .tx-skip { color: #888; }
            .tx-create { }
            code { background: #f5f5f5; padding: .1rem .3rem; border-radius: 3px; }
          </style>
        </head>
        <body>
          <h1>Firefly III Custom Uploader</h1>
          {{body}}
        </body>
        </html>
        """;

    public static string UploadForm(string? error)
    {
        var sb = new StringBuilder();

        if (error is not null)
            sb.Append($"<div class=\"banner banner-error\">{Encode(error)}</div>");

        sb.Append("<h2>Upload a statement</h2>");
        sb.Append(
            "<form method=\"post\" action=\"/upload\" enctype=\"multipart/form-data\">" +
            "<div class=\"field\">" +
            "<label for=\"file\">Statement PDF</label>" +
            "<input type=\"file\" id=\"file\" name=\"file\" accept=\".pdf\" required>" +
            "</div>" +
            "<div class=\"actions\"><button type=\"submit\" class=\"btn\">Upload →</button></div>" +
            "</form>");

        return Page("Upload", sb.ToString());
    }

    public static string SelectForm(
        string token,
        IReadOnlyList<IStatementParser> parsers,
        string? selectedFormatId,
        IReadOnlyList<Account> accounts,
        string? selectedAccountId,
        string? error)
    {
        var sb = new StringBuilder();

        if (error is not null)
            sb.Append($"<div class=\"banner banner-error\">{Encode(error)}</div>");

        sb.Append("<h2>Select format and account</h2>");
        sb.Append(
            "<form method=\"post\" action=\"/select\">" +
            $"<input type=\"hidden\" name=\"token\" value=\"{Encode(token)}\">" +
            "<div class=\"field\">" +
            "<label for=\"formatId\">Statement format</label>" +
            "<select id=\"formatId\" name=\"formatId\" required>");

        foreach (var p in parsers)
        {
            var selected = p.FormatId == selectedFormatId ? " selected" : "";
            sb.Append($"<option value=\"{Encode(p.FormatId)}\"{selected}>{Encode(p.DisplayName)}</option>");
        }

        sb.Append(
            "</select></div>" +
            "<div class=\"field\">" +
            "<label for=\"accountId\">Firefly III asset account</label>" +
            "<select id=\"accountId\" name=\"accountId\" required>");

        if (accounts.Count == 0)
        {
            sb.Append("<option value=\"\" disabled selected>No asset accounts found — check your Firefly III connection</option>");
        }
        else
        {
            foreach (var a in accounts)
            {
                var label = a.Attributes.Iban is not null
                    ? $"{a.Attributes.Name} ({a.Attributes.Iban})"
                    : a.Attributes.Name;
                var selected = a.Id == selectedAccountId ? " selected" : "";
                sb.Append($"<option value=\"{Encode(a.Id)}\"{selected}>{Encode(label)}</option>");
            }
        }

        sb.Append(
            "</select></div>" +
            "<div class=\"actions\"><button type=\"submit\" class=\"btn\">Continue →</button></div>" +
            "</form>");

        return Page("Select format", sb.ToString());
    }

    public static string Preview(UploadPlan plan, string token)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Review transactions</h2>");

        var toCreate = plan.Items.Count(i => i.Decision == UploadDecision.Create);
        var duplicates = plan.Items.Count(i => i.Decision == UploadDecision.SkipDuplicate);

        sb.Append($"<p>Account: <strong>{Encode(plan.AssetAccountName)}</strong> &mdash; ");
        sb.Append($"<strong>{toCreate}</strong> to create");
        if (duplicates > 0) sb.Append($", {duplicates} already in Firefly");
        sb.Append("</p>");

        if (plan.Items.Count == 0)
        {
            sb.Append("<p><em>No transactions parsed from this file.</em></p>");
            sb.Append("<p><a href=\"/\" class=\"btn btn-secondary\">← Upload another file</a></p>");
            return Page("Preview", sb.ToString());
        }

        sb.Append(
            "<form method=\"post\" action=\"/submit\">" +
            $"<input type=\"hidden\" name=\"token\" value=\"{Encode(token)}\">" +
            "<table><thead><tr><th>Include</th><th>Date</th><th>Type</th><th>Amount</th><th>Description</th><th>Status</th></tr></thead><tbody>");

        for (int i = 0; i < plan.Items.Count; i++)
        {
            var item = plan.Items[i];
            var (rowClass, statusText, checkboxAttrs) = item.Decision switch
            {
                UploadDecision.Create => ("tx-create", "Will import", "checked"),
                UploadDecision.SkipDuplicate => ("tx-skip", "Already in Firefly", ""),
                _ => ("", "Unknown", "disabled"),
            };
            var direction = item.Transaction.IsDebit ? "Debit" : "Credit";
            var amount = item.Transaction.Amount.ToString("0.00");

            sb.Append(
                $"<tr class=\"{rowClass}\">" +
                $"<td><input type=\"checkbox\" name=\"include\" value=\"{i}\" {checkboxAttrs}></td>" +
                $"<td>{item.Transaction.Date:yyyy-MM-dd}</td>" +
                $"<td>{direction}</td>" +
                $"<td>€ {Encode(amount)}</td>" +
                $"<td>{Encode(item.Transaction.Description)}</td>" +
                $"<td>{statusText}</td>" +
                $"</tr>");
        }

        sb.Append("</tbody></table>");
        sb.Append($"<div class=\"actions\">");
        sb.Append($"<button type=\"submit\" class=\"btn\">Submit {toCreate} transaction(s) to Firefly III</button>");
        sb.Append($"<a href=\"/download-csv/{Encode(token)}\" class=\"btn btn-secondary\">Download CSV</a>");
        sb.Append($"<a href=\"/\" class=\"btn btn-secondary\">← Start over</a>");
        sb.Append($"</div></form>");

        return Page("Review", sb.ToString());
    }

    public static string Result(UploadResult result)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Upload complete</h2>");
        sb.Append($"<p>Run label: <code>{Encode(result.RunTag)}</code></p>");
        sb.Append("<table><thead><tr><th>Outcome</th><th>Count</th></tr></thead><tbody>");
        sb.Append($"<tr><td>Created</td><td><strong>{result.Created}</strong></td></tr>");
        sb.Append($"<tr><td>Already in Firefly (skipped)</td><td>{result.SkippedDuplicate}</td></tr>");
        sb.Append($"<tr><td>Excluded by you</td><td>{result.SkippedExcluded}</td></tr>");
        if (result.Errors > 0)
            sb.Append($"<tr><td><strong>Errors</strong></td><td><strong style=\"color:#dc3545\">{result.Errors}</strong></td></tr>");
        sb.Append("</tbody></table>");

        if (result.Errors > 0)
            sb.Append("<div class=\"banner banner-error\">Some transactions failed to create. Check the application logs.</div>");
        else
            sb.Append("<div class=\"banner banner-success\">Done!</div>");

        sb.Append("<div class=\"actions\"><a href=\"/\" class=\"btn btn-secondary\">← Upload another file</a></div>");
        return Page("Done", sb.ToString());
    }

    public static string Error(string message) =>
        Page("Error",
            $"<div class=\"banner banner-error\">{Encode(message)}</div>" +
            "<p><a href=\"/\" class=\"btn btn-secondary\">← Back</a></p>");

    private static string Encode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
