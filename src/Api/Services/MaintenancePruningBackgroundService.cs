namespace TicketSpan.Api.Services;

public sealed class MaintenancePruningBackgroundService : BackgroundService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<MaintenancePruningBackgroundService> logger;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);

    public MaintenancePruningBackgroundService(IServiceProvider serviceProvider, ILogger<MaintenancePruningBackgroundService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var cleanupService = scope.ServiceProvider.GetRequiredService<ITableCleanupService>();
                var summary = await cleanupService.RunFullCleanupAsync(stoppingToken);
                logger.LogInformation("Background table cleanup executed successfully: {Tokens} tokens, {Auths} authorizations, {Logs} audit logs deleted.",
                    summary.OpenIddictTokensDeleted, summary.OpenIddictAuthorizationsDeleted, summary.AuditLogsDeleted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during background table maintenance cleanup execution.");
            }
        }
    }
}
