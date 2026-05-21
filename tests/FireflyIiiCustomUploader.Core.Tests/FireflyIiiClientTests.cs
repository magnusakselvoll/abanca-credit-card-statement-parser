using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FireflyIiiCustomUploader.Core.FireflyIii;
using FireflyIiiCustomUploader.Core.FireflyIii.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace FireflyIiiCustomUploader.Core.Tests;

[TestClass]
public class FireflyIiiClientTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private static HttpClient MakeClient(params (string path, object body)[] responses)
    {
        var handler = new SequentialHandler(responses);
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
    }

    [TestMethod]
    public async Task GetAssetAccountsAsync_SinglePage_ReturnsAccounts()
    {
        var page = new PaginatedResponse<Account>(
            [new Account("1", new AccountAttributes("My Card", "asset", "ES001"))],
            new PaginationMeta(new Pagination(1, 1)));

        using var client = MakeClient(("api/v1/accounts?type=asset&page=1", page));
        var sut = new FireflyIiiClient(client, NullLogger<FireflyIiiClient>.Instance);

        var result = await sut.GetAssetAccountsAsync();

        Assert.HasCount(1, result);
        Assert.AreEqual("1", result[0].Id);
        Assert.AreEqual("ES001", result[0].Attributes.Iban);
    }

    [TestMethod]
    public async Task GetAssetAccountsAsync_MultiPage_ReturnsAllAccounts()
    {
        var page1 = new PaginatedResponse<Account>(
            [new Account("1", new AccountAttributes("Card A", "asset", null))],
            new PaginationMeta(new Pagination(1, 2)));
        var page2 = new PaginatedResponse<Account>(
            [new Account("2", new AccountAttributes("Card B", "asset", null))],
            new PaginationMeta(new Pagination(2, 2)));

        using var client = MakeClient(
            ("api/v1/accounts?type=asset&page=1", page1),
            ("api/v1/accounts?type=asset&page=2", page2));
        var sut = new FireflyIiiClient(client, NullLogger<FireflyIiiClient>.Instance);

        var result = await sut.GetAssetAccountsAsync();

        Assert.HasCount(2, result);
    }

    [TestMethod]
    public async Task GetTransactionsAsync_SinglePage_ReturnsTransactions()
    {
        var txAttrs = new TransactionSplitAttributes("ext-1", new DateOnly(2025, 4, 15), "42.50", "SHOP");
        var group = new TransactionGroupAttributes([txAttrs]);
        var tx = new Transaction("100", group);
        var page = new PaginatedResponse<Transaction>(
            [tx],
            new PaginationMeta(new Pagination(1, 1)));

        using var client = MakeClient(
            ("api/v1/accounts/42/transactions?start=2025-04-01&end=2025-04-30&page=1", page));
        var sut = new FireflyIiiClient(client, NullLogger<FireflyIiiClient>.Instance);

        var result = await sut.GetTransactionsAsync(
            "42", new DateOnly(2025, 4, 1), new DateOnly(2025, 4, 30));

        Assert.HasCount(1, result);
        Assert.AreEqual("ext-1", result[0].Attributes.Transactions[0].ExternalId);
    }

    [TestMethod]
    public async Task CreateTransactionAsync_PostsToCorrectEndpoint()
    {
        var capturedRequest = default(HttpRequestMessage);
        var handler = new CapturingHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var sut = new FireflyIiiClient(client, NullLogger<FireflyIiiClient>.Instance);

        var split = new TransactionSplit(
            "withdrawal", new DateOnly(2025, 4, 15), "42.50", "SHOP",
            "EUR", "ext-1", "My Card", null, ["run-tag"], null);
        var store = new TransactionStore(false, [split]);

        await sut.CreateTransactionAsync(store);

        Assert.IsNotNull(capturedRequest);
        Assert.AreEqual(HttpMethod.Post, capturedRequest.Method);
        Assert.AreEqual("api/v1/transactions", capturedRequest.RequestUri?.PathAndQuery.TrimStart('/'));
    }

    private sealed class SequentialHandler : HttpMessageHandler
    {
        private readonly Queue<(string PathQuery, object Body)> _responses;

        public SequentialHandler(IEnumerable<(string, object)> responses)
        {
            _responses = new Queue<(string, object)>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.IsTrue(_responses.Count > 0, $"Unexpected request: {request.RequestUri}");
            var (expectedPath, body) = _responses.Dequeue();
            var actualPath = request.RequestUri?.PathAndQuery.TrimStart('/') ?? string.Empty;
            Assert.AreEqual(expectedPath, actualPath);

            var json = JsonSerializer.Serialize(body, JsonOpts);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
