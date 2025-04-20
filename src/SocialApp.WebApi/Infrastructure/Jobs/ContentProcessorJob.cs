using SocialApp.WebApi.Features.Content.Services;

namespace SocialApp.WebApi.Infrastructure.Jobs;

public sealed class ContentProcessorJob : BackgroundService
{
    private readonly IContentStreamProcessorService _contents;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ContentProcessorJob> _logger;

    public ContentProcessorJob(IContentStreamProcessorService contents, IConfiguration configuration, ILogger<ContentProcessorJob> logger)
    {
        _contents = contents;
        _configuration = configuration;
        _logger = logger;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting content processor.");
        return base.StartAsync(cancellationToken);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping content processor.");
        return base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _contents.ProcessChangeFeedAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error processing the content stream.");
                await Task.Delay(1_000, stoppingToken);
            }
        }
    }
}