using EventosPeruIA.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventosPeruIA.Tests;

public sealed class WorkerTests
{
    [Fact]
    public async Task WorkerStartsAutomaticallyAndRetriesAfterProviderFailure()
    {
        var index = new RetryIndex();
        using var provider = new ServiceCollection().AddSingleton<IRagIndexService>(index).BuildServiceProvider();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["AI:ReindexIntervalSeconds"] = "10" }).Build();
        using var worker = new RagIndexWorker(provider.GetRequiredService<IServiceScopeFactory>(), config,
            NullLogger<RagIndexWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            await index.Retried.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(index.Calls >= 2);
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await worker.StopAsync(shutdown.Token);
        }
    }
    private sealed class RetryIndex : IRagIndexService
    {
        public int Calls;
        public TaskCompletionSource Retried { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<int> ReindexEventsAsync()
        {
            if (Interlocked.Increment(ref Calls) == 1) throw new HttpRequestException("simulated outage");
            Retried.TrySetResult(); return Task.FromResult(0);
        }
    }
}
