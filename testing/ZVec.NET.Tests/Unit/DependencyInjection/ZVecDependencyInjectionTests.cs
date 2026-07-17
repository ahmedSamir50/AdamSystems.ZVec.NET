using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ZVec.NET.DependencyInjection;

namespace ZVec.NET.Tests.Unit.DependencyInjection;

public class ZVecDependencyInjectionTests
{
    [Fact]
    public void AddZVec_ShouldRegisterFactoryAsSingleton()
    {
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;
        try
        {
            var services = new ServiceCollection();
            ZVecDefaults.Version.BypassAbiCheck = true;

            services.AddZVec(options =>
            {
                options.QueryThreads = 4;
            });

            var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IZvecFactory));
            descriptor.Should().NotBeNull();
            descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        }
        finally
        {
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    [Fact]
    public void AddZVecCollection_ShouldRegisterKeyedCollection()
    {
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;
        try
        {
            var services = new ServiceCollection();
            ZVecDefaults.Version.BypassAbiCheck = true;

            services.AddZVec();
            services.AddZVecCollection("test-collection", options =>
            {
                options.Path = "test_path";
            });

            var descriptor = services.FirstOrDefault(d =>
                d.ServiceType == typeof(IZvecCollection) &&
                d.IsKeyedService &&
                Equals(d.ServiceKey, "test-collection"));

            descriptor.Should().NotBeNull();
            descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        }
        finally
        {
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    [Fact]
    public void AddZVec_WithConfiguration_BindsOptionsSection()
    {
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;
        try
        {
            ZVecDefaults.Version.BypassAbiCheck = true;

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"{ZVecDefaults.GlobalOptions.ConfigurationSection}:QueryThreads"] = "8",
                    [$"{ZVecDefaults.GlobalOptions.ConfigurationSection}:LogLevel"] = "Debug"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddZVec(configuration);

            using var provider = services.BuildServiceProvider();
            var options = provider.GetRequiredService<IOptions<ZVecOptions>>().Value;
            options.QueryThreads.Should().Be(8);
            options.LogLevel.Should().Be(ZVecLogLevel.Debug);
        }
        finally
        {
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    [Fact]
    public async Task HostShutdown_DisposesFactory()
    {
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;
        try
        {
            ZVecDefaults.Version.BypassAbiCheck = true;

            using var host = Host.CreateDefaultBuilder()
                .ConfigureServices(services => services.AddZVec())
                .Build();

            await host.StartAsync(TestContext.Current.CancellationToken);
            var factory = host.Services.GetRequiredService<IZvecFactory>();
            factory.IsInitialized.Should().BeTrue();

            await host.StopAsync(TestContext.Current.CancellationToken);
            factory.IsInitialized.Should().BeFalse();
        }
        finally
        {
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    [Fact]
    public async Task ZVecHealthCheck_WhenInitialized_ReturnsHealthy()
    {
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;
        try
        {
            ZVec.NET.Interop.NativeLibraryResolver.UseRealLibrary();
            ZVecDefaults.Version.BypassAbiCheck = true;

            var services = new ServiceCollection();
            services.AddZVec();
            await using var provider = services.BuildServiceProvider();

            var factory = provider.GetRequiredService<IZvecFactory>();
            factory.IsInitialized.Should().BeTrue();

            var check = new ZVecHealthCheck(factory);
            var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
            result.Status.Should().Be(HealthStatus.Healthy);
        }
        finally
        {
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    [Fact]
    public async Task ZVecHealthCheck_WhenNotInitialized_ReturnsUnhealthy()
    {
        var factory = new ZVecFactory();
        var check = new ZVecHealthCheck(factory);

        var result = await check.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain(ZVecDefaults.Errors.FactoryNotInitialized);
    }
}
