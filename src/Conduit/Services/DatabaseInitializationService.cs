using System;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Conduit.Services;

public class DatabaseInitializationService(IServiceProvider serviceProvider,
                                         ILogger<DatabaseInitializationService> logger,
                                         IHostEnvironment environment) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() && !environment.IsStaging())
        {
            logger.LogInformation("Skipping EnsureCreated in current environment.");
            return;
        }

        logger.LogInformation("Database initialization starting.");
        var maxRetries = 10;
        var delay = TimeSpan.FromSeconds(40);

        for (var i = 0; i < maxRetries; i++)
        {
            if (cancellationToken.IsCancellationRequested) {return;}

            try
            {
                using var scope = serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ConduitContext>();
                var ensureCreatedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                ensureCreatedCts.CancelAfter(TimeSpan.FromMinutes(3));

                await dbContext.Database.EnsureCreatedAsync(ensureCreatedCts.Token);
                logger.LogInformation("Database schema ensured/checked successfully.");
                return;
            }
            catch when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Database initialization cancelled by application shutdown.");
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to ensure database (attempt {i + 1}/{maxRetries}).");
                if (i < maxRetries - 1)
                {
                    logger.LogInformation($"Retrying in {delay.TotalSeconds} seconds...");
                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                        delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 1.5, 250));
                    }
                    catch (OperationCanceledException)
                    {
                         logger.LogWarning("Database initialization retry delay cancelled by application shutdown.");
                         return;
                    }
                }
                else
                {
                    logger.LogCritical("Max retries reached. Could not ensure database. The application might not function correctly.");
                    return;
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
