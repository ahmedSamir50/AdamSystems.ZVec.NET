using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using ZVec.NET.DependencyInjection;

namespace ZVec.NET.Tests.Unit.DependencyInjection;

public class ZVecDependencyInjectionTests
{
    [Fact]
    public void AddZVec_ShouldRegisterFactoryAsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        ZVecDefaults.Version.BypassAbiCheck = true; // Avoid DLL load error during test

        // Act
        services.AddZVec(options =>
        {
            options.QueryThreads = 4;
        });

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IZvecFactory));
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddZVecCollection_ShouldRegisterKeyedCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        ZVecDefaults.Version.BypassAbiCheck = true;

        services.AddZVec();
        services.AddZVecCollection("test-collection", options =>
        {
            options.Path = "test_path";
        });

        // Act
        var descriptor = services.FirstOrDefault(d => 
            d.ServiceType == typeof(IZvecCollection) && 
            d.IsKeyedService && 
            Equals(d.ServiceKey, "test-collection"));

        // Assert
        descriptor.Should().NotBeNull();
        descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
    }
}
