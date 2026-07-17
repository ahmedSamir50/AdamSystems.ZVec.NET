using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ZVec.NET.Interop;

/// <summary>
/// Handles platform-specific loading of the ZVec native library
/// (<c>zvec_c_api.dll</c> / <c>libzvec_c_api.so</c> / <c>libzvec_c_api.dylib</c>).
/// </summary>
internal static class NativeLibraryResolver
{
    /// <summary>
    /// Atomic resolver state — all mutable fields are bundled into a single
    /// reference that is swapped via <see cref="Interlocked.Exchange(ref object?, object)"/>
    /// to prevent torn reads across threads.
    /// </summary>
    private sealed class ResolverState
    {
        public bool UseMock;
        public string? MockLibraryPath;
    }

    private static volatile ResolverState _resolverState = new();
    private static bool _isRegistered = false;
    private static IntPtr _cachedHandle = IntPtr.Zero;
    internal static bool IsLoaded => _cachedHandle != IntPtr.Zero;

    /// <summary>Returns the cached native module handle, or <see cref="IntPtr.Zero"/> if not loaded.</summary>
    internal static IntPtr LoadedHandle => _cachedHandle;
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
    /// Atomic swap ensures concurrent readers never see a torn state.
    /// </summary>
    internal static void SetMockLibrary(string? libraryPath)
    {
        Interlocked.Exchange(ref _resolverState, new ResolverState
        {
            UseMock = libraryPath != null,
            MockLibraryPath = libraryPath
        });
    }

    /// <summary>
    /// Restores the resolver to load the real native library.
    /// Atomic swap ensures concurrent readers never see a torn state.
    /// </summary>
    internal static void UseRealLibrary()
    {
        Interlocked.Exchange(ref _resolverState, new ResolverState());
    }

    /// <summary>
    /// File name of the shared library for the current OS
    /// (e.g. <c>zvec_c_api.dll</c>, <c>libzvec_c_api.so</c>, <c>libzvec_c_api.dylib</c>).
    /// </summary>
    internal static string GetNativeLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
            return NativeMethods.LibraryName + ".dll";
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsMacCatalyst())
            return "lib" + NativeMethods.LibraryName + ".dylib";
        // Linux, Android, FreeBSD, etc.
        return "lib" + NativeMethods.LibraryName + ".so";
    }

    /// <summary>
    /// Maps a runtime RID (e.g. <c>win10-x64</c>, <c>ubuntu.22.04-x64</c>) to the portable
    /// RID folder we ship under <c>runtimes/{rid}/native/</c>.
    /// </summary>
    internal static string GetPortableRid(string? runtimeIdentifier = null)
    {
        string rid = runtimeIdentifier ?? RuntimeInformation.RuntimeIdentifier;
        if (string.IsNullOrEmpty(rid))
            return rid;

        // Prefer exact known portable folders when present later via probe; normalize common cases.
        if (rid.StartsWith("win", StringComparison.OrdinalIgnoreCase))
        {
            if (rid.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                return "win-arm64";
            if (rid.Contains("x86", StringComparison.OrdinalIgnoreCase) && !rid.Contains("x64", StringComparison.OrdinalIgnoreCase))
                return "win-x86";
            return "win-x64";
        }

        if (rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase) ||
            rid.StartsWith("macos", StringComparison.OrdinalIgnoreCase))
        {
            return rid.Contains("arm64", StringComparison.OrdinalIgnoreCase) ||
                   rid.Contains("aarch64", StringComparison.OrdinalIgnoreCase)
                ? "osx-arm64"
                : "osx-x64";
        }

        if (rid.StartsWith("maccatalyst", StringComparison.OrdinalIgnoreCase))
        {
            return rid.Contains("arm64", StringComparison.OrdinalIgnoreCase) ||
                   rid.Contains("aarch64", StringComparison.OrdinalIgnoreCase)
                ? "maccatalyst-arm64"
                : "maccatalyst-x64";
        }

        if (rid.StartsWith("ios", StringComparison.OrdinalIgnoreCase))
        {
            if (rid.Contains("simulator", StringComparison.OrdinalIgnoreCase))
            {
                return rid.Contains("x64", StringComparison.OrdinalIgnoreCase) ||
                       rid.Contains("x86_64", StringComparison.OrdinalIgnoreCase)
                    ? "iossimulator-x64"
                    : "iossimulator-arm64";
            }

            return "ios-arm64";
        }

        if (rid.StartsWith("android", StringComparison.OrdinalIgnoreCase))
        {
            if (rid.Contains("x64", StringComparison.OrdinalIgnoreCase) ||
                rid.Contains("x86_64", StringComparison.OrdinalIgnoreCase))
                return "android-x64";
            if (rid.Contains("x86", StringComparison.OrdinalIgnoreCase))
                return "android-x86";
            if (rid.Contains("arm", StringComparison.OrdinalIgnoreCase) &&
                !rid.Contains("arm64", StringComparison.OrdinalIgnoreCase))
                return "android-arm";
            return "android-arm64";
        }

        if (rid.Contains("linux", StringComparison.OrdinalIgnoreCase) ||
            rid.Contains("ubuntu", StringComparison.OrdinalIgnoreCase) ||
            rid.Contains("debian", StringComparison.OrdinalIgnoreCase) ||
            rid.Contains("alpine", StringComparison.OrdinalIgnoreCase))
        {
            bool musl = rid.Contains("musl", StringComparison.OrdinalIgnoreCase) ||
                        rid.Contains("alpine", StringComparison.OrdinalIgnoreCase);
            bool arm64 = rid.Contains("arm64", StringComparison.OrdinalIgnoreCase) ||
                         rid.Contains("aarch64", StringComparison.OrdinalIgnoreCase);
            if (musl)
                return arm64 ? "linux-musl-arm64" : "linux-musl-x64";
            return arm64 ? "linux-arm64" : "linux-x64";
        }

        return rid;
    }

    /// <summary>
    /// Attempts to load the native library by probing well-known paths,
    /// then falls back to standard .NET resolution.
    /// </summary>
    internal static void EnsureLoaded()
    {
        if (_cachedHandle != IntPtr.Zero)
            return;

        var state = _resolverState;
        if (state.UseMock && state.MockLibraryPath is not null)
        {
            // Resolve throws DllNotFoundException when the mock path cannot be loaded.
            IntPtr handle = Resolve(NativeMethods.LibraryName, typeof(NativeMethods).Assembly, null);
            if (handle == IntPtr.Zero)
            {
                throw new DllNotFoundException(
                    string.Format(
                        ZVecDefaults.Errors.NativeLibraryLoadFailed,
                        RuntimeInformation.RuntimeIdentifier,
                        ZVecDefaults.Errors.NativeLibraryLoadHint));
            }

            _cachedHandle = handle;
            return;
        }

        foreach (var path in EnumerateProbePaths())
        {
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
            {
                _cachedHandle = handle;
                return;
            }
        }

        // Fall back to standard .NET native library resolution (deps.json / libzvec_c_api.so on Android).
        try
        {
            _ = NativeLibrary.Load(NativeMethods.LibraryName, typeof(NativeMethods).Assembly, null);
        }
        catch (DllNotFoundException)
        {
            // Call our resolver directly to throw the custom RID exception.
            Resolve(NativeMethods.LibraryName, typeof(NativeMethods).Assembly, null);
            throw;
        }
    }

    private static IEnumerable<string> EnumerateProbePaths()
    {
        string baseDir = AppContext.BaseDirectory;
        string fileName = GetNativeLibraryFileName();
        string runtimeRid = RuntimeInformation.RuntimeIdentifier;
        string portableRid = GetPortableRid(runtimeRid);

        var ridCandidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            runtimeRid,
            portableRid
        };

        foreach (var rid in ridCandidates)
        {
            if (string.IsNullOrEmpty(rid))
                continue;
            yield return Path.Combine(baseDir, "runtimes", rid, "native", fileName);
        }

        // Flat next to the managed assembly (local deploy / some MAUI layouts).
        yield return Path.Combine(baseDir, fileName);

        // Android often extracts JNI libs under lib/{abi}/ relative to the app.
        if (OperatingSystem.IsAndroid())
        {
            string abi = portableRid switch
            {
                "android-arm64" => "arm64-v8a",
                "android-x64" => "x86_64",
                "android-arm" => "armeabi-v7a",
                "android-x86" => "x86",
                _ => "arm64-v8a"
            };
            yield return Path.Combine(baseDir, "lib", abi, fileName);
        }
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != NativeMethods.LibraryName) return IntPtr.Zero;

        // Single volatile read — always consistent snapshot.
        var state = _resolverState;

        // Mock path takes priority — when mock is set, never fall through to real library.
        if (state.UseMock && state.MockLibraryPath is not null)
        {
            if (NativeLibrary.TryLoad(state.MockLibraryPath, out var handle))
                return handle;

            throw new DllNotFoundException(
                string.Format(
                    ZVecDefaults.Errors.NativeLibraryLoadFailed,
                    RuntimeInformation.RuntimeIdentifier,
                    ZVecDefaults.Errors.NativeLibraryLoadHint));
        }

        // Return cached handle if already loaded.
        if (_cachedHandle != IntPtr.Zero)
            return _cachedHandle;

        foreach (var path in EnumerateProbePaths())
        {
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var probed))
                return _cachedHandle = probed;
        }

        // Fall back to standard native resolution rules.
        if (NativeLibrary.TryLoad(name, assembly, searchPath, out var realHandle))
            return _cachedHandle = realHandle;

        throw new DllNotFoundException(
            string.Format(
                ZVecDefaults.Errors.NativeLibraryLoadFailed,
                RuntimeInformation.RuntimeIdentifier,
                ZVecDefaults.Errors.NativeLibraryLoadHint));
    }
}
