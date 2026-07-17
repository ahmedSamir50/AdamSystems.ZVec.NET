using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ZVec.NET.DependencyInjection;
using ZVec.NET.Mapping;

namespace ZVec.NET.Tests.Unit.DependencyInjection;

public class ZVecTypedDependencyInjectionTests
{
    [Fact]
    public void AddZVecCollectionOfT_RegistersTypedAndKeyedServices()
    {
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;
        try
        {
            ZVecDefaults.Version.BypassAbiCheck = true;
            var services = new ServiceCollection();
            services.AddZVec();
            services.AddZVecCollection<DiProduct>(o => o.Path = "typed_path");

            services.Should().Contain(d => d.ServiceType == typeof(IZvecCollection<DiProduct>) && !d.IsKeyedService);
            services.Should().Contain(d =>
                d.ServiceType == typeof(IZvecCollection<DiProduct>) &&
                d.IsKeyedService &&
                Equals(d.ServiceKey, "DiProduct"));
            services.Should().Contain(d =>
                d.ServiceType == typeof(IZvecCollection) &&
                d.IsKeyedService &&
                Equals(d.ServiceKey, "DiProduct"));
        }
        finally
        {
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    [Fact]
    public void AddZVecCollectionOfT_CreateFalse_DoesNotForceSchema()
    {
        var previousBypass = ZVecDefaults.Version.BypassAbiCheck;
        try
        {
            ZVecDefaults.Version.BypassAbiCheck = true;
            var services = new ServiceCollection();
            services.AddZVec();
            services.AddZVecCollection<DiProduct>(o =>
            {
                o.Path = "open_only";
                o.Create = false;
            });

            services.Should().Contain(d => d.ServiceType == typeof(IZvecCollection<DiProduct>));
        }
        finally
        {
            ZVecDefaults.Version.BypassAbiCheck = previousBypass;
        }
    }

    private sealed class DiProduct
    {
        public string Id { get; set; } = "";
        [ZVecVector(4)]
        public ReadOnlyMemory<float> Embedding { get; set; }
    }
}
