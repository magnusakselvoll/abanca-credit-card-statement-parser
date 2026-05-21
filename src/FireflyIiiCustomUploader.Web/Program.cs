using FireflyIiiCustomUploader.Core;
using FireflyIiiCustomUploader.Core.Options;
using FireflyIiiCustomUploader.Core.Parsing;
using FireflyIiiCustomUploader.Web.Web;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddFireflyIiiCustomUploader(builder.Configuration);
builder.Services.AddSingleton<IPdfTextExtractor, FireflyIiiCustomUploader.Core.Parsing.PdfPigTextExtractor>();
builder.Services.AddSingleton<ReviewState>();

var app = builder.Build();

var options = app.Services.GetRequiredService<IOptions<FireflyIiiCustomUploaderOptions>>().Value;
app.Urls.Clear();
app.Urls.Add(options.WebListenUrl);

UploadEndpoints.Map(app);
PreviewEndpoints.Map(app);

app.Run();
