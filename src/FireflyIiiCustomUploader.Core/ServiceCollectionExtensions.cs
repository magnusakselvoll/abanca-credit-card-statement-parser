using System.Net;
using System.Net.Http.Headers;
using FireflyIiiCustomUploader.Core.FireflyIii;
using FireflyIiiCustomUploader.Core.Options;
using FireflyIiiCustomUploader.Core.Parsing;
using FireflyIiiCustomUploader.Core.Parsing.Abanca;
using FireflyIiiCustomUploader.Core.Parsing.Advanzia;
using FireflyIiiCustomUploader.Core.Sync;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace FireflyIiiCustomUploader.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFireflyIiiCustomUploader(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<FireflyIiiCustomUploaderOptions>(
            configuration.GetSection("FireflyIiiCustomUploader"));

        services.AddHttpClient<IFireflyIiiClient, FireflyIiiClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<FireflyIiiCustomUploaderOptions>>().Value;
            client.BaseAddress = new Uri(opts.FireflyIiiUrl.TrimEnd('/') + '/');
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", opts.FireflyIiiToken);
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
        .AddResilienceHandler("fireflyiii", ConfigureResiliencePipeline);

        services.AddSingleton<IStatementParser, AbancaStatementParser>();
        services.AddSingleton<IStatementParser, AbancaWebCopyStatementParser>();
        services.AddSingleton<IStatementParser, AdvanziaStatementParser>();
        services.AddSingleton<StatementParserRegistry>();
        services.AddTransient<UploadPlanner>();
        services.AddTransient<UploadExecutor>();

        return services;
    }

    private static void ConfigureResiliencePipeline(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        builder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldRetryAfterHeader = true,
            ShouldHandle = args => args.Outcome switch
            {
                { Result: { StatusCode: HttpStatusCode.TooManyRequests } } => PredicateResult.True(),
                { Result: { StatusCode: HttpStatusCode.ServiceUnavailable } } => PredicateResult.True(),
                _ => new ValueTask<bool>(HttpClientResiliencePredicates.IsTransient(args.Outcome)),
            },
        });

        builder.AddTimeout(TimeSpan.FromSeconds(30));
    }
}
