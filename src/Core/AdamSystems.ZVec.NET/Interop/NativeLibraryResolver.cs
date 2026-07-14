using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// Handles platform-specific loading of the ZVec native library.
/// </summary>
internal static class NativeLibraryResolver
{
    private static bool _useMock = false;
    private static string? _mockLibraryPath = null;
    private static bool _isRegistered = false;
    private static readonly object _lock = new();

#pragma warning disable CA2255
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Initialize()
    {
        EnsureRegistered();
    }

    /// <summary>
    /// Guarantees that the DLL import resolver is registered.
    /// </summary>
    internal static void EnsureRegistered()
    {
        if (_isRegistered) return;
        lock (_lock)
        {
            if (_isRegistered) return;
            NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
            _isRegistered = true;
        }
    }

    /// <summary>
    /// Switches the resolver to use a mock library path for testing.
    /// </summary>
    internal static void SetMockLibrary(string? libraryPath)
    {
        _mockLibraryPath = libraryPath;
        _useMock = libraryPath != null;
    }

    /// <summary>
    /// Restores the resolver to load the real native library.
    /// </summary>
    internal static void UseRealLibrary()
    {
        _useMock = false;
    }

    internal static void EnsureLoaded()
    {
        try
        {
            // Calling NativeLibrary.Load validates existence.
            _ = NativeLibrary.Load(NativeMethods.LibraryName, typeof(NativeMethods).Assembly, null);
        }
        catch (DllNotFoundException)
        {
            // Call our resolver directly to throw the custom RID exception
            Resolve(NativeMethods.LibraryName, typeof(NativeMethods).Assembly, null);
            throw;
        }
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != NativeMethods.LibraryName) return IntPtr.Zero;

        if (_useMock && _mockLibraryPath is not null)
        {
            if (NativeLibrary.TryLoad(_mockLibraryPath, out var handle))
                return handle;
        }

        // Fall back to standard native resolution rules
        if (NativeLibrary.TryLoad(name, assembly, searchPath, out var realHandle))
            return realHandle;

        throw new DllNotFoundException(
            $"ZVec native library not found for RID '{RuntimeInformation.RuntimeIdentifier}'. " +
            "Ensure the AdamSystems.ZVec.NET NuGet package supports your platform.");
    }
}
