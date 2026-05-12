using System.Globalization;
using System.Text;
using AbancaCardParser.Cli;
using AbancaCardParser.Core.Output;
using AbancaCardParser.Core.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.AddConfiguration(configuration.GetSection("Logging"));
});
var logger = loggerFactory.CreateLogger("AbancaCardParser");

var options = new ParserOptions();
configuration.GetSection("AbancaCardParser").Bind(options);

var textExtractor = new PdfPigTextExtractor();
var parser = new StatementTextParser();
var writer = new BankCsvWriter();

var inputDir = new DirectoryInfo(options.InputDir);
var outputDir = new DirectoryInfo(options.OutputDir);
var logDir = new DirectoryInfo(options.LogDir);

if (!inputDir.Exists)
{
    logger.LogError("Input directory not found: {InputDir}", inputDir.FullName);
    return 1;
}

outputDir.Create();
logDir.Create();

var pdfFiles = inputDir.GetFiles("*.pdf");
if (pdfFiles.Length == 0)
{
    logger.LogInformation("No PDF files found in {InputDir}", inputDir.FullName);
    return 0;
}

bool anyError = false;

foreach (var pdfFile in pdfFiles)
{
    var successLogPath = Path.Combine(logDir.FullName, pdfFile.Name + ".success.log");

    if (File.Exists(successLogPath))
    {
        logger.LogInformation("Skipped (already processed): {File}", pdfFile.Name);
        continue;
    }

    var errorLogPath = Path.Combine(logDir.FullName, pdfFile.Name + ".error.log");
    var csvPath = Path.Combine(outputDir.FullName, pdfFile.Name + ".csv");

    try
    {
        logger.LogInformation("Processing {File}", pdfFile.Name);

        IReadOnlyList<string> lines;
        using (var stream = pdfFile.OpenRead())
            lines = textExtractor.ExtractLines(stream);

        var statement = parser.Parse(lines);
        var csv = writer.Write(statement);

        File.WriteAllText(csvPath, csv, Encoding.UTF8);

        var logLines = BuildSuccessLog(statement);
        File.WriteAllText(successLogPath, string.Join(Environment.NewLine, logLines) + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (File.Exists(errorLogPath))
            File.Delete(errorLogPath);

        logger.LogInformation("Completed {File}: {Count} transaction(s)", pdfFile.Name, statement.Transactions.Count);
    }
    catch (Exception ex)
    {
        anyError = true;
        File.WriteAllText(errorLogPath, ex.ToString(), new UTF8Encoding(false));
        logger.LogError(ex, "Failed to process {File}", pdfFile.Name);
    }
}

return anyError ? 1 : 0;

static List<string> BuildSuccessLog(AbancaCardParser.Core.Models.CardStatement statement)
{
    var totalDebits = statement.Transactions.Where(t => t.IsDebit).Sum(t => t.Amount);
    var totalCredits = statement.Transactions.Where(t => !t.IsDebit).Sum(t => t.Amount);

    var lines = new List<string>
    {
        $"Processed at: {DateTimeOffset.UtcNow:O}",
        $"Transactions: {statement.Transactions.Count}",
        $"Total debits (D): {FormatSpanish(totalDebits)}",
        $"Total credits (H): {FormatSpanish(totalCredits)}",
    };

    if (statement.StatedTotal.HasValue)
        lines.Add($"PDF stated total (TOTAL OPERACIONES TARJETA): {FormatSpanish(statement.StatedTotal.Value)}");

    return lines;
}

static string FormatSpanish(decimal value) =>
    value.ToString("0.00", CultureInfo.InvariantCulture).Replace(".", ",");
