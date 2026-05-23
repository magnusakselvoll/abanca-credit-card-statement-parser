using System.Collections.Concurrent;

namespace FireflyIiiCustomUploader.Web.Web;

public record PendingUpload(IReadOnlyList<string> Lines, string? DetectedFormatId);

public sealed class PendingUploadStore
{
    private readonly ConcurrentDictionary<string, (PendingUpload Upload, DateTimeOffset CreatedAt)> _pending = new();

    public string Add(PendingUpload upload)
    {
        PurgeExpired();
        var token = Guid.NewGuid().ToString("N");
        _pending[token] = (upload, DateTimeOffset.UtcNow);
        return token;
    }

    public PendingUpload? GetIfValid(string token)
    {
        if (_pending.TryGetValue(token, out var entry))
        {
            if (DateTimeOffset.UtcNow - entry.CreatedAt < TimeSpan.FromMinutes(15))
                return entry.Upload;
        }
        return null;
    }

    private void PurgeExpired()
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(15);
        foreach (var key in _pending.Keys)
        {
            if (_pending.TryGetValue(key, out var entry) && entry.CreatedAt < cutoff)
                _pending.TryRemove(key, out _);
        }
    }
}
