using System.Collections.Concurrent;
using ZVec.NET.Exceptions;
using ZVec.NET.Interop;

namespace ZVec.NET;

/// <summary>
/// Process-wide factory for initializing ZVec and opening vector collections.
/// </summary>
/// <remarks>
/// <para>
/// Thread safety: Lifecycle state transitions (<c>Initialize</c>, <c>Shutdown</c>)
/// are guarded by a standard process-wide lock. This ensures safe initialization
/// of the native singleton even under highly concurrent test runners.
/// </para>
/// <para>
/// Open collections are tracked with <b>strong</b> references in a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> so that <see cref="Shutdown"/> can
/// dispose every outstanding collection before calling native <c>zvec_shutdown</c>.
/// Callers should still dispose collections they own; Shutdown is a safety net for process teardown.
/// </para>
/// <para>
/// Async factory APIs are cancellation-aware wrappers around synchronous native calls.
/// </para>
/// </remarks>
public sealed class ZVecFactory : IZvecFactory
{
    // =========================================================================
    // State machine — transitions only go forward: Uninitialized → Initialized → ShutDown.
    // =========================================================================
    private int _state = FactoryState.Uninitialized;
    private static int _globalNativeInitCount = 0;
    private static readonly object _globalInitLock = new();

    private static class FactoryState
    {
        internal const int Uninitialized = 0;
        internal const int Initialized   = 1;
        internal const int ShutDown      = 2;
    }

    /// <summary>Returns <c>true</c> if the factory has been successfully initialized.</summary>
    public bool IsInitialized
    {
        get
        {
            lock (_globalInitLock) return _state == FactoryState.Initialized;
        }
    }

    /// <summary>Returns <c>true</c> if the native library is currently loaded and initialized globally.</summary>
    internal static bool IsNativeLibraryInitialized
    {
        get
        {
            lock (_globalInitLock) return _globalNativeInitCount > 0;
        }
    }

    // =========================================================================
    // Open collection tracking — strong references so Shutdown can close them
    // before zvec_shutdown. ConcurrentDictionary provides lock-free reads.
    // =========================================================================
    internal readonly ConcurrentDictionary<nint, ZVecCollection> OpenCollections
        = new();

    // Shutdown cancellation — signalled when Shutdown() is called.
    private CancellationTokenSource _shutdownCts = new();

    /// <summary>
    /// Optional process-wide throttle for native calls when <see cref="ZVecOptions.MaxConcurrentNativeCalls"/> &gt; 0.
    /// </summary>
    private SemaphoreSlim? _nativeCallGate;

    // =========================================================================
    // Initialization
    // =========================================================================

    /// <inheritdoc/>
    public void Initialize(ZVecOptions? options = null)
    {
        lock (_globalInitLock)
        {
            if (_state != FactoryState.Uninitialized)
            {
                return; // Already initialized or shut down — no-op.
            }

            try
            {
                if (_globalNativeInitCount == 0)
                {
                    NativeLibraryResolver.EnsureLoaded();
                    ApplyNativeConfig(options);
                }
                else
                {
                    NativeLibraryResolver.EnsureLoaded();
                }

                CheckAbiVersion();

                int maxNative = options?.MaxConcurrentNativeCalls
                    ?? ZVecDefaults.GlobalOptions.MaxConcurrentNativeCalls;
                if (maxNative > 0)
                    _nativeCallGate = new SemaphoreSlim(maxNative, maxNative);

                _globalNativeInitCount++;
                _state = FactoryState.Initialized;
            }
            catch
            {
                // Throw and leave state as Uninitialized
                throw;
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask InitializeAsync(ZVecOptions? options = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // zvec_initialize is synchronous at the native level; no async work to offload.
        Initialize(options);
        return ValueTask.CompletedTask;
    }

    // =========================================================================
    // Shutdown
    // =========================================================================

    /// <inheritdoc/>
    /// <remarks>
    /// Cancels outstanding call tokens, disposes every tracked open collection (close-only),
    /// then shuts down the native library when this is the last live factory.
    /// </remarks>
    public void Shutdown()
    {
        ZVecCollection[] toClose;
        lock (_globalInitLock)
        {
            if (_state != FactoryState.Initialized)
            {
                return; // Not initialized, already shutting down, or already shut down — no-op.
            }

            // Mark ShutDown immediately so concurrent Shutdown calls bail out while we close collections
            // with the native library still initialized.
            _state = FactoryState.ShutDown;
            _shutdownCts.Cancel();
            toClose = OpenCollections.Values.ToArray();
            OpenCollections.Clear();
        }

        // Dispose outside the init lock to avoid deadlocking with IsInitialized / SafeHandle guards.
        foreach (var col in toClose)
        {
            try
            {
                col.Dispose();
            }
            catch
            {
                // Best-effort close during teardown.
            }
        }

        lock (_globalInitLock)
        {
            if (_state != FactoryState.ShutDown)
            {
                return;
            }

            // Only shutdown the native library when the last factory is shut down.
            _globalNativeInitCount--;
            if (_globalNativeInitCount == 0)
            {
                try
                {
                    NativeMethods.zvec_shutdown();
                }
                catch (DllNotFoundException)
                {
                    // Native library already unloaded or resolver poisoned.
                }
            }

            _nativeCallGate?.Dispose();
            _nativeCallGate = null;

            // State goes back to Uninitialized so it can be re-initialized if needed (especially for tests)
            _shutdownCts = new CancellationTokenSource();
            _state = FactoryState.Uninitialized;
        }
    }

    /// <inheritdoc/>
    public ValueTask ShutdownAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Shutdown();
        return ValueTask.CompletedTask;
    }

    // =========================================================================
    // Collection creation / opening
    // =========================================================================

    /// <inheritdoc/>
    public IZvecCollection CreateAndOpen(string path, ZVecCollectionSchema schema, ZVecCollectionOptions? options = null)
    {
        ThrowIfNotInitialized();
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(schema);

        using var nativeSchema = new Internal.NativeCollectionSchemaBuilder(schema);
        using var nativeOptions = options != null ? new Internal.NativeCollectionOptionsBuilder(options) : null;

        var rc = NativeMethods.zvec_collection_create_and_open(
            path, nativeSchema.Handle, nativeOptions?.Handle ?? IntPtr.Zero, out IntPtr handle);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(CreateAndOpen));

        var collection = new ZVecCollection(handle, path, schema, _shutdownCts.Token, this, options);
        TrackCollection(collection, handle);
        return collection;
    }

    /// <inheritdoc/>
    public ValueTask<IZvecCollection> CreateAndOpenAsync(
        string path,
        ZVecCollectionSchema schema,
        ZVecCollectionOptions? options = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(CreateAndOpen(path, schema, options));
    }

    /// <inheritdoc/>
    public IZvecCollection Open(string path, ZVecCollectionOptions? options = null)
    {
        ThrowIfNotInitialized();
        ArgumentException.ThrowIfNullOrEmpty(path);

        using var nativeOptions = options != null ? new Internal.NativeCollectionOptionsBuilder(options) : null;

        var rc = NativeMethods.zvec_collection_open(path, nativeOptions?.Handle ?? IntPtr.Zero, out IntPtr handle);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Open));

        var collection = new ZVecCollection(handle, path, schema: null, _shutdownCts.Token, this, options);
        TrackCollection(collection, handle);
        return collection;
    }

    /// <inheritdoc/>
    public ValueTask<IZvecCollection> OpenAsync(
        string path,
        ZVecCollectionOptions? options = null,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Open(path, options));
    }

    /// <inheritdoc/>
    public string GetNativeVersion()
    {
        ThrowIfNotInitialized();
        return NativeMethods.GetVersionString();
    }

    /// <inheritdoc/>
    public ZVecNativeAbiInfo GetAbiInfo()
    {
        int requiredMajor = ZVecDefaults.Version.ExpectedMajor;
        string requiredMinimum =
            $"{requiredMajor}.{ZVecDefaults.Version.ExpectedMinor}.{ZVecDefaults.Version.ExpectedPatch}";

        if (!Interop.NativeLibraryResolver.IsLoaded)
        {
            return new ZVecNativeAbiInfo(
                requiredMinimum,
                requiredMajor,
                FoundVersion: null,
                FoundMajor: null,
                IsCompatible: false);
        }

        string found = NativeMethods.GetVersionString();
        int foundMajor = NativeMethods.zvec_get_version_major();
        bool meetsMinimum = NativeMethods.zvec_check_version(
            ZVecDefaults.Version.ExpectedMajor,
            ZVecDefaults.Version.ExpectedMinor,
            ZVecDefaults.Version.ExpectedPatch);
        bool compatible = ZVecNativeAbi.IsCompatible(meetsMinimum, foundMajor, requiredMajor);

        return new ZVecNativeAbiInfo(
            requiredMinimum,
            requiredMajor,
            found,
            foundMajor,
            compatible);
    }

    // =========================================================================
    // IDisposable / IAsyncDisposable
    // =========================================================================

    /// <inheritdoc/>
    public void Dispose() => Shutdown();

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        Shutdown();
        return ValueTask.CompletedTask;
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static void CheckAbiVersion()
    {
        if (ZVecDefaults.Version.BypassAbiCheck)
            return;

        int requiredMajor = ZVecDefaults.Version.ExpectedMajor;
        int requiredMinor = ZVecDefaults.Version.ExpectedMinor;
        int requiredPatch = ZVecDefaults.Version.ExpectedPatch;

        bool meetsMinimum = NativeMethods.zvec_check_version(requiredMajor, requiredMinor, requiredPatch);
        int foundMajor = NativeMethods.zvec_get_version_major();

        if (!ZVecNativeAbi.IsCompatible(meetsMinimum, foundMajor, requiredMajor))
        {
            string found = NativeMethods.GetVersionString();
            string minimum = $"{requiredMajor}.{requiredMinor}.{requiredPatch}";
            throw new ZVecAbiMismatchException(minimum, requiredMajor, found);
        }
    }

    /// <summary>Acquires the optional native-call throttle (no-op when unlimited).</summary>
    internal void EnterNativeCall(CancellationToken cancellationToken = default)
    {
        _nativeCallGate?.Wait(cancellationToken);
    }

    /// <summary>Releases the optional native-call throttle (no-op when unlimited).</summary>
    internal void ExitNativeCall()
    {
        _nativeCallGate?.Release();
    }

    private static void ApplyNativeConfig(ZVecOptions? options)
    {
        if (options is null)
        {
            // Use default native config (NULL → native uses its own defaults).
            ZVecError.ThrowIfFailed((ZVecErrorCode)NativeMethods.zvec_initialize(IntPtr.Zero), nameof(Initialize));
            return;
        }

        IntPtr cfg = NativeMethods.zvec_config_data_create();
        try
        {
            if (options.QueryThreads > 0)
                NativeMethods.zvec_config_data_set_query_thread_count(cfg, (uint)options.QueryThreads);

            if (options.MemoryLimitMb.HasValue)
                NativeMethods.zvec_config_data_set_memory_limit(cfg, (ulong)options.MemoryLimitMb.Value * 1024 * 1024);

            IntPtr logCfg = options.LogType == ZVecLogType.File
                ? NativeMethods.zvec_config_log_create_file(
                    (int)options.LogLevel,
                    options.LogDir ?? "./logs",
                    options.LogBasename ?? "zvec.log",
                    options.LogFileSizeMb,
                    options.LogOverdueDays)
                : NativeMethods.zvec_config_log_create_console((int)options.LogLevel);

            try
            {
                NativeMethods.zvec_config_data_set_log_config(cfg, logCfg);
                ZVecError.ThrowIfFailed(
                    (ZVecErrorCode)NativeMethods.zvec_initialize(cfg),
                    nameof(Initialize));
            }
            finally
            {
                NativeMethods.zvec_config_log_destroy(logCfg);
            }
        }
        finally
        {
            NativeMethods.zvec_config_data_destroy(cfg);
        }
    }

    private void TrackCollection(ZVecCollection col, nint handle)
    {
        OpenCollections.TryAdd(handle, col);
    }

    private void ThrowIfNotInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException(ZVecDefaults.Errors.FactoryNotInitialized);
    }
}
