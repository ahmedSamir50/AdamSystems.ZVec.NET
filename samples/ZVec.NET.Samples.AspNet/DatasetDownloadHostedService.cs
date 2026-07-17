using ZVec.NET.Samples.Shared.Data;

namespace ZVec.NET.Samples.AspNet;

/// <summary>Downloads MB sample packs in the background without blocking listen.</summary>
public sealed class DatasetDownloadHostedService : IHostedService
{
    private readonly ILogger<DatasetDownloadHostedService> _logger;
    private CancellationTokenSource? _cts;
    private Task? _running;

    public DatasetDownloadHostedService(ILogger<DatasetDownloadHostedService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = _cts.Token;
        _running = Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(msg => _logger.LogInformation("[datasets] {Message}", msg));
                await DatasetDownloader.EnsureAllAsync(progress, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // host shutting down
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[datasets] background download failed; T0 fixtures still work");
            }
        }, ct);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is null)
            return;

        await _cts.CancelAsync().ConfigureAwait(false);
        if (_running is not null)
        {
            try
            {
                await _running.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        _cts.Dispose();
        _cts = null;
    }
}
