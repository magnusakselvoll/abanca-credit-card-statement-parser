using FireflyIiiCustomUploader.Core;
using FireflyIiiCustomUploader.Core.Options;
using FireflyIiiCustomUploader.Core.Parsing;
using FireflyIiiCustomUploader.Web.Web;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddSimpleConsole(o =>
    {
        o.IncludeScopes = true;
        o.SingleLine = true;
    });
}
else
{
    builder.Logging.AddJsonConsole(o =>
    {
        o.IncludeScopes = true;
        o.UseUtcTimestamp = true;
        o.TimestampFormat = "o";
    });
}

builder.Services.AddFireflyIiiCustomUploader(builder.Configuration);
builder.Services.AddSingleton<IPdfTextExtractor, FireflyIiiCustomUploader.Core.Parsing.PdfPigTextExtractor>();
builder.Services.AddSingleton<ReviewState>();
builder.Services.AddSingleton<PendingUploadStore>();

var app = builder.Build();

var options = app.Services.GetRequiredService<IOptions<FireflyIiiCustomUploaderOptions>>().Value;
app.Urls.Clear();
app.Urls.Add(options.WebListenUrl);

UploadEndpoints.Map(app);
PreviewEndpoints.Map(app);

app.Run();
