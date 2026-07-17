using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZVec.NET.DependencyInjection;

/// <summary>
/// Extension methods for registering ZVec services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ZVecServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IZvecFactory"/> as a singleton service and initializes it
    /// from the <c>ZVec</c> configuration section.
    /// </summary>
    public static IServiceCollection AddZVec(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        return services.AddZVec(configuration.GetSection(ZVecDefaults.GlobalOptions.ConfigurationSection));
    }

    /// <summary>
    /// Registers the <see cref="IZvecFactory"/> as a singleton service and initializes it
    /// from the supplied configuration section.
    /// </summary>
    public static IServiceCollection AddZVec(this IServiceCollection services, IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(section);

        services.AddOptions<ZVecOptions>().Bind(section);
        return services.AddZVecCore();
    }

    /// <summary>
    /// Registers the <see cref="IZvecFactory"/> as a singleton service and initializes it.
    /// </summary>
    public static IServiceCollection AddZVec(
        this IServiceCollection services,
        Action<ZVecOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
            services.Configure(configure);

        return services.AddZVecCore();
    }

    /// <summary>
    /// Registers an <see cref="IZvecCollection"/> as a keyed singleton service.
    /// </summary>
    public static IServiceCollection AddZVecCollection(
        this IServiceCollection services,
        string serviceKey,
        Action<ZVecCollectionRegistrationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serviceKey);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddKeyedSingleton(serviceKey, (sp, key) =>
        {
            var factory = sp.GetRequiredService<IZvecFactory>();

            var regOptions = new ZVecCollectionRegistrationOptions();
            configure(regOptions);

            if (string.IsNullOrWhiteSpace(regOptions.Path))
            {
                throw new ArgumentException(ZVecDefaults.Errors.CollectionPathRequired, nameof(configure));
            }

            if (regOptions.Schema != null)
            {
                return factory.CreateAndOpen(regOptions.Path, regOptions.Schema, regOptions.Options);
            }

            return factory.Open(regOptions.Path, regOptions.Options);
        });

        return services;
    }

    private static IServiceCollection AddZVecCore(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ZVecFactoryLifetimeService>());
        services.TryAddSingleton<IZvecFactory>(CreateAndInitializeFactory);
        return services;
    }

    private static IZvecFactory CreateAndInitializeFactory(IServiceProvider sp)
    {
        var logger = sp.GetService<ILogger<ZVecFactory>>();
        var options = sp.GetService<IOptions<ZVecOptions>>()?.Value ?? new ZVecOptions();

        logger?.LogInformation("Initializing ZVec factory.");
        var factory = new ZVecFactory();
        factory.Initialize(options);
        logger?.LogInformation("ZVec factory initialized.");
        return factory;
    }
}
