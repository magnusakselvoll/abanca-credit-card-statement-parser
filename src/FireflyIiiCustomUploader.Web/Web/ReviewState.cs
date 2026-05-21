using System.Collections.Concurrent;
using FireflyIiiCustomUploader.Core.Sync;

namespace FireflyIiiCustomUploader.Web.Web;

public sealed class ReviewState
{
    private readonly ConcurrentDictionary<string, (UploadPlan Plan, DateTimeOffset CreatedAt)> _pending = new();

    public string Add(UploadPlan plan)
    {
        PurgeExpired();
        var token = Guid.NewGuid().ToString("N");
        _pending[token] = (plan, DateTimeOffset.UtcNow);
        return token;
    }

    public UploadPlan? GetIfValid(string token)
    {
        if (_pending.TryGetValue(token, out var entry))
        {
            if (DateTimeOffset.UtcNow - entry.CreatedAt < TimeSpan.FromMinutes(15))
                return entry.Plan;
        }
        return null;
    }

    public UploadPlan? TakeIfValid(string token)
    {
        if (_pending.TryRemove(token, out var entry))
        {
            if (DateTimeOffset.UtcNow - entry.CreatedAt < TimeSpan.FromMinutes(15))
                return entry.Plan;
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
