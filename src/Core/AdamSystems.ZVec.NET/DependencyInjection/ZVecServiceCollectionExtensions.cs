using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AdamSystems.ZVec.NET.DependencyInjection;

/// <summary>
/// Extension methods for registering ZVec services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ZVecServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IZvecFactory"/> as a singleton service and initializes it.
    /// </summary>
    public static IServiceCollection AddZVec(
        this IServiceCollection services,
        Action<ZVecOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IZvecFactory>(sp =>
        {
            var options = new ZVecOptions();
            configure?.Invoke(options);

            var factory = new ZVecFactory();
            factory.Initialize(options);
            return factory;
        });

        return services;
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

        services.AddKeyedSingleton<IZvecCollection>(serviceKey, (sp, key) =>
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
            else
            {
                return factory.Open(regOptions.Path, regOptions.Options);
            }
        });

        return services;
    }
}
