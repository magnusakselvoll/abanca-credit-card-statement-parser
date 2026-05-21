using System.Net.Http.Json;
using System.Text.Json;
using FireflyIiiCustomUploader.Core.FireflyIii.Models;
using Microsoft.Extensions.Logging;

namespace FireflyIiiCustomUploader.Core.FireflyIii;

public sealed class FireflyIiiClient : IFireflyIiiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new LenientDateOnlyConverter() },
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<FireflyIiiClient> _logger;

    public FireflyIiiClient(HttpClient httpClient, ILogger<FireflyIiiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Account>> GetAssetAccountsAsync(CancellationToken cancellationToken = default)
    {
        var all = new List<Account>();
        var page = 1;

        while (true)
        {
            var response = await GetJsonAsync<PaginatedResponse<Account>>(
                $"api/v1/accounts?type=asset&page={page}", cancellationToken);

            all.AddRange(response.Data);

            if (response.Meta.Pagination.CurrentPage >= response.Meta.Pagination.TotalPages)
                break;

            page++;
        }

        _logger.LogInformation("Retrieved {Count} asset accounts from Firefly III.", all.Count);
        return all;
    }

    public async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(
        string accountId,
        DateOnly dateFrom,
        DateOnly dateTo,
        CancellationToken cancellationToken = default)
    {
        var all = new List<Transaction>();
        var page = 1;

        while (true)
        {
            var response = await GetJsonAsync<PaginatedResponse<Transaction>>(
                $"api/v1/accounts/{accountId}/transactions?start={dateFrom:yyyy-MM-dd}&end={dateTo:yyyy-MM-dd}&page={page}",
                cancellationToken);

            all.AddRange(response.Data);

            if (response.Meta.Pagination.CurrentPage >= response.Meta.Pagination.TotalPages)
                break;

            page++;
        }

        return all;
    }

    private async Task<T> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var statusInt = (int)response.StatusCode;
            var location = response.Headers.Location?.ToString();
            var redirectSuffix = location is not null ? $" Redirected to: {location}" : string.Empty;
            var preview = body.Length > 300 ? body[..300] : body;
            var message = $"Firefly III returned HTTP {statusInt} {response.ReasonPhrase} for {url}.{redirectSuffix} Body: {preview}";
            _logger.LogWarning("Firefly III request failed: {StatusCode} for {Url}{Redirect}", statusInt, url, redirectSuffix);
            throw new InvalidOperationException(message);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOptions)
                ?? throw new InvalidOperationException("Firefly III returned an empty response body.");
        }
        catch (JsonException ex)
        {
            var preview = body.Length > 300 ? body[..300] : body;
            var message = $"Firefly III returned non-JSON content for {url} (check URL and token). Response starts with: {preview}";
            _logger.LogWarning("Firefly III returned non-JSON for {Url}", url);
            throw new InvalidOperationException(message, ex);
        }
    }

    public async Task CreateTransactionAsync(TransactionStore transaction, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/transactions", transaction, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

        _logger.LogInformation("Created transaction with external_id: {ExternalId}.",
            transaction.Transactions.FirstOrDefault()?.ExternalId);
    }
}
