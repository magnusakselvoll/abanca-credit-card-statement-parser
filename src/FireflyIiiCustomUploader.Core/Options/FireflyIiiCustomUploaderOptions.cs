namespace FireflyIiiCustomUploader.Core.Options;

public class FireflyIiiCustomUploaderOptions
{
    public string FireflyIiiUrl { get; init; } = string.Empty;
    public string FireflyIiiToken { get; init; } = string.Empty;
    public string WebListenUrl { get; init; } = "http://0.0.0.0:8080";
    public string RunTagPrefix { get; init; } = "ffcu-upload";
}
