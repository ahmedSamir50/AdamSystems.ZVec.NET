using ZVec.NET.Interop;
using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Interop;

[Collection("ResolverStateTests")]
public class NativeLibraryResolverTests
{
    [Fact]
    public void NativeLibraryResolver_MissingLibrary_ThrowsDllNotFound()
    {
        if (NativeLibraryResolver.IsLoaded) return; // Skip test: Library already loaded in this process.
        // US-E6.1: Force load failure by setting mock path to non-existent library
        NativeLibraryResolver.SetMockLibrary("non_existent_library_file_path_123.dll");
        try
        {
            var act = () => NativeLibraryResolver.EnsureLoaded();
            act.Should().Throw<DllNotFoundException>()
               .WithMessage("*ZVec native library not found*");
        }
        finally
        {
            // Reset resolver back to standard configuration
            NativeLibraryResolver.UseRealLibrary();
        }
    }
}
