using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZVec.NET.DependencyInjection;

internal sealed class ZVecFactoryLifetimeService : IHostedService
{
    private readonly IZvecFactory _factory;
    private readonly ILogger<ZVecFactory>? _logger;

    public ZVecFactoryLifetimeService(IZvecFactory factory, ILogger<ZVecFactory>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Shutting down ZVec factory.");
        _factory.Shutdown();
        if (_factory is IDisposable disposable)
            disposable.Dispose();
        return Task.CompletedTask;
    }
}
