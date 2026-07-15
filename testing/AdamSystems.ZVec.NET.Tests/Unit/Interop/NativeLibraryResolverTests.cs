using AdamSystems.ZVec.NET.Interop;
using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit.Interop;

public class NativeLibraryResolverTests
{
    [Fact]
    public void NativeLibraryResolver_MissingLibrary_ThrowsDllNotFound()
    {
        // US-E6.1: Force load failure by setting mock path to non-existent library
        NativeLibraryResolver.SetMockLibrary("non_existent_library_file_path_123.dll");

        var act = () => NativeLibraryResolver.EnsureLoaded();
        act.Should().Throw<DllNotFoundException>()
           .WithMessage("*ZVec native library not found*");

        // Reset resolver back to standard configuration
        NativeLibraryResolver.UseRealLibrary();
    }
}
