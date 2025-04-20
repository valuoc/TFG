using SocialApp.WebApi.Features._Shared.Services;
using SocialApp.WebApi.Features.Account.Services;

namespace SocialApp.WebApi.Infrastructure.Jobs;

public sealed class PendingAccountCleanJob : BackgroundService
{
    private readonly IAccountService _accounts;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PendingAccountCleanJob> _logger;

    private int PendingAccountCheckIntervalSeconds 
        => _configuration.GetValue("PendingAccountCheckIntervalSeconds", 60 * 5);

    public PendingAccountCleanJob(IAccountService accounts, IConfiguration configuration, ILogger<PendingAccountCleanJob> logger)
    {
        _accounts = accounts;
        _configuration = configuration;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting pending account cleaning job.");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping pending account cleaning job.");
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var context = new OperationContext(stoppingToken);
        
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(PendingAccountCheckIntervalSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = 0;
                do
                {
                    removed = await _accounts.RemovedExpiredPendingAccountsAsync(TimeSpan.FromSeconds(PendingAccountCheckIntervalSeconds), context);   
                } while (removed > 0);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error trying remove pending account cleaning job.");
            }
            
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}