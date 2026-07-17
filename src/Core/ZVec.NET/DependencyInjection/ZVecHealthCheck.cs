using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ZVec.NET.DependencyInjection;

/// <summary>
/// Reports whether the process-wide <see cref="IZvecFactory"/> has been initialized.
/// </summary>
public sealed class ZVecHealthCheck : IHealthCheck
{
    private readonly IZvecFactory _factory;

    /// <summary>Creates a health check bound to the registered factory singleton.</summary>
    public ZVecHealthCheck(IZvecFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    /// <inheritdoc/>
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_factory.IsInitialized)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(ZVecDefaults.Errors.FactoryNotInitialized));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
