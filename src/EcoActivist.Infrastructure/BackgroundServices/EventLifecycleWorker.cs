using EcoActivist.Application.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcoActivist.Infrastructure.BackgroundServices;

public sealed class EventLifecycleWorkerOptions
{
    public const string SectionName = "Workers:EventLifecycle";

    public bool Enabled { get; set; } = true;

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);
}

public sealed class EventLifecycleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly EventLifecycleWorkerOptions _options;
    private readonly ILogger<EventLifecycleWorker> _logger;

    public EventLifecycleWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IOptions<EventLifecycleWorkerOptions> options,
        ILogger<EventLifecycleWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("EventLifecycleWorker отключён конфигурацией");
            return;
        }

        _logger.LogInformation("EventLifecycleWorker запущен, интервал {Interval}", _options.Interval);

        using var timer = new PeriodicTimer(_options.Interval, _timeProvider);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<EventLifecycleService>();
            await service.ProcessAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка выполнения EventLifecycleWorker");
        }
    }
}
