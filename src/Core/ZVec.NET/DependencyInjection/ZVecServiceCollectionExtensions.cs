using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZVec.NET.Mapping;

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
            return OpenCollection(factory, regOptions);
        });

        return services;
    }

    /// <summary>
    /// Registers <see cref="IZvecCollection{T}"/> as a singleton (and keyed by the mapped collection name).
    /// Schema defaults to <see cref="ZVecCollectionSchemaBuilder.From{T}"/> when not supplied.
    /// </summary>
    /// <typeparam name="T">Mapped document type.</typeparam>
    public static IServiceCollection AddZVecCollection<T>(
        this IServiceCollection services,
        Action<ZVecCollectionRegistrationOptions> configure)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var model = ZVecTypeModel.Get<T>();
        var serviceKey = model.CollectionName;

        services.AddSingleton<IZvecCollection<T>>(sp =>
        {
            var factory = sp.GetRequiredService<IZvecFactory>();
            var regOptions = new ZVecCollectionRegistrationOptions();
            configure(regOptions);

            if (regOptions.Create)
                regOptions.Schema ??= ZVecCollectionSchemaBuilder.From<T>().Build();
            else
                regOptions.Schema = null;

            var untyped = OpenCollection(factory, regOptions);
            return new ZVecCollection<T>(untyped);
        });

        services.AddKeyedSingleton<IZvecCollection<T>>(serviceKey, (sp, _) =>
            sp.GetRequiredService<IZvecCollection<T>>());

        services.AddKeyedSingleton<IZvecCollection>(serviceKey, (sp, _) =>
            sp.GetRequiredService<IZvecCollection<T>>().Untyped);

        return services;
    }

    private static IZvecCollection OpenCollection(IZvecFactory factory, ZVecCollectionRegistrationOptions regOptions)
    {
        if (string.IsNullOrWhiteSpace(regOptions.Path))
            throw new ArgumentException(ZVecDefaults.Errors.CollectionPathRequired, nameof(regOptions));

        var options = regOptions.ResolveOptions();
        if (regOptions.Schema != null)
            return factory.CreateAndOpen(regOptions.Path, regOptions.Schema, options);

        return factory.Open(regOptions.Path, options);
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
