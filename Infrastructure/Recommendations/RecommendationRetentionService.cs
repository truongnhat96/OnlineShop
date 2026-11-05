using Infrastructure.SqlServer.DataContext;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Recommendations;

public sealed class RecommendationRetentionService(IServiceScopeFactory scopes, ILogger<RecommendationRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
                await db.RecommendationEvents.Where(e => e.At < DateTimeOffset.UtcNow.AddDays(-90)).ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Recommendation retention failed: {ErrorType}", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
