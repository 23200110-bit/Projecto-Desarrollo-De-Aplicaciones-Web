namespace EventosPeruIA.Api.Services;

// Reconciliación periódica: también detecta cambios hechos fuera de esta API.
public sealed class RagIndexWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<RagIndexWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = Math.Clamp(configuration.GetValue<int?>("AI:ReindexIntervalSeconds") ?? 60, 10, 3600);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IRagIndexService>().ReindexEventsAsync();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "No se pudo sincronizar el índice RAG; se reintentará.");
            }
            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
        }
    }
}
