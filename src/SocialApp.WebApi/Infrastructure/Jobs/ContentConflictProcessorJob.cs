using SocialApp.WebApi.Features.Content.Services;

namespace SocialApp.WebApi.Infrastructure.Jobs;

public sealed class ContentConflictProcessorJob : BackgroundService
{
    private readonly IContentConflictResolutionService _service;
    private readonly ILogger<ContentConflictProcessorJob> _logger;

    public ContentConflictProcessorJob(
        IContentConflictResolutionService service,
        ILogger<ContentConflictProcessorJob> logger)
    {
        _service = service;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ContentConflictProcessorJob is starting.");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ContentConflictProcessorJob is stopping.");
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _service.ProcessConflictResolutionFeedAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing the conflict feed.");
                await Task.Delay(1_000, stoppingToken);
            }
        }
        
    }
}