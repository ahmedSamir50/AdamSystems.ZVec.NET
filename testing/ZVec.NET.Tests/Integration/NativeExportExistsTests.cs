using System.Reflection;
using System.Runtime.InteropServices;
using FluentAssertions;
using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// Honest gate: every [LibraryImport] on NativeMethods must resolve in the loaded zvec_c_api DLL.
/// Would have failed CI for orphan names like zvec_doc_get_sparse_vector_field.
/// </summary>
public class NativeExportExistsTests : IClassFixture<ZVecRealNativeFixture>
{
    private readonly ZVecRealNativeFixture _fixture;

    public NativeExportExistsTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void All_LibraryImport_EntryPoints_Exist_In_NativeDll()
    {
        _fixture.SkipIfNotAvailable();

        NativeLibraryResolver.EnsureLoaded();
        IntPtr handle = NativeLibraryResolver.LoadedHandle;
        handle.Should().NotBe(IntPtr.Zero, "native library must be loaded for export checks");

        var methods = typeof(NativeMethods)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(m => m.Name.StartsWith("zvec_", StringComparison.Ordinal))
            .Select(m => m.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        methods.Should().NotBeEmpty();

        var missing = new List<string>();
        foreach (var name in methods)
        {
            if (!NativeLibrary.TryGetExport(handle, name, out _))
                missing.Add(name);
        }

        missing.Should().BeEmpty(
            "every NativeMethods LibraryImport must exist in c_api / the loaded DLL. Missing: {0}",
            string.Join(", ", missing));
    }

    [Fact]
    public void Version_Exports_Are_Callable()
    {
        _fixture.SkipIfNotAvailable();

        NativeMethods.zvec_get_version_major().Should().BeGreaterThanOrEqualTo(0);
        NativeMethods.zvec_get_version_minor().Should().BeGreaterThanOrEqualTo(0);
        NativeMethods.zvec_get_version_patch().Should().BeGreaterThanOrEqualTo(0);
        NativeMethods.GetVersionString().Should().NotBeNullOrWhiteSpace();
    }
}
