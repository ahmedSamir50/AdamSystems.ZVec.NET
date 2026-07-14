using System.Collections.Concurrent;
using AdamSystems.ZVec.NET.Exceptions;
using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET;

/// <summary>
/// Process-wide factory for initializing ZVec and opening vector collections.
/// </summary>
/// <remarks>
/// <para>
/// Thread safety: Lifecycle state transitions (<c>Initialize</c>, <c>Shutdown</c>)
/// are guarded by <see cref="Interlocked.CompareExchange(ref int, int, int)"/> — a
/// lock-free, atomic CAS. No custom reader-writer lock is used because the native
/// <c>GlobalConfig</c> singleton already guards initialization with
/// <c>std::atomic&lt;bool&gt;</c> internally, making a managed lock redundant.
/// </para>
/// <para>
/// Open collection handles are tracked in a <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// so that <see cref="Shutdown"/> can signal all outstanding collections.
/// </para>
/// </remarks>
public sealed class ZVecFactory : IZvecFactory
{
    // =========================================================================
    // State machine — transitions only go forward: Uninitialized → Initialized → ShutDown.
    // Using int so Interlocked.CompareExchange can operate on it.
    // =========================================================================
    private static int _state = FactoryState.Uninitialized;

    private static class FactoryState
    {
        internal const int Uninitialized = 0;
        internal const int Initialized   = 1;
        internal const int ShutDown      = 2;
    }

    /// <summary>Returns <c>true</c> if the factory has been successfully initialized.</summary>
    public static bool IsInitialized =>
        Volatile.Read(ref _state) == FactoryState.Initialized;

    // =========================================================================
    // Open collection tracking — weak references so GC can still finalize
    // collections independently if the user forgets to dispose them.
    // ConcurrentDictionary provides lock-free reads; write locks are per-slot.
    // =========================================================================
    internal static readonly ConcurrentDictionary<nint, ZVecCollection> OpenCollections
        = new();

    // Shutdown cancellation — signalled when Shutdown() is called.
    private static CancellationTokenSource _shutdownCts = new();

    // =========================================================================
    // Initialization
    // =========================================================================

    /// <inheritdoc/>
    public void Initialize(ZVecOptions? options = null)
    {
        // Atomic CAS: only the first caller transitions Uninitialized → Initialized.
        // All subsequent callers (including concurrent ones) find the state is already
        // Initialized or ShutDown and return immediately — the native library handles
        // its own idempotency with std::atomic<bool> internally.
        if (Interlocked.CompareExchange(ref _state, FactoryState.Initialized, FactoryState.Uninitialized)
            != FactoryState.Uninitialized)
        {
            return; // Already initialized or shut down — no-op.
        }

        try
        {
            CheckAbiVersion();
            ApplyNativeConfig(options);
        }
        catch
        {
            // Roll back state so the caller can retry.
            Interlocked.Exchange(ref _state, FactoryState.Uninitialized);
            throw;
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
    public void Shutdown()
    {
        // Only transition from Initialized → ShutDown exactly once.
        if (Interlocked.CompareExchange(ref _state, FactoryState.ShutDown, FactoryState.Initialized)
            != FactoryState.Initialized)
        {
            return; // Not initialized, or already shut down — no-op.
        }

        // Signal all tracked collections that the factory is shutting down.
        _shutdownCts.Cancel();
        OpenCollections.Clear();

        NativeMethods.zvec_shutdown();

        // State goes back to Uninitialized so it can be re-initialized if needed (especially for tests)
        _shutdownCts = new CancellationTokenSource();
        Interlocked.Exchange(ref _state, FactoryState.Uninitialized);
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

        var collection = new ZVecCollection(handle, path, schema, _shutdownCts.Token);
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

        var collection = new ZVecCollection(handle, path, schema: null, _shutdownCts.Token);
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

        bool compatible = NativeMethods.zvec_check_version(
            ZVecDefaults.Version.ExpectedMajor,
            ZVecDefaults.Version.ExpectedMinor,
            ZVecDefaults.Version.ExpectedPatch);

        if (!compatible)
        {
            string found = NativeMethods.GetVersionString();
            string expected = $"{ZVecDefaults.Version.ExpectedMajor}.{ZVecDefaults.Version.ExpectedMinor}.{ZVecDefaults.Version.ExpectedPatch}";
            throw new ZVecAbiMismatchException(expected, found);
        }
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

    private static void TrackCollection(ZVecCollection col, nint handle)
    {
        OpenCollections.TryAdd(handle, col);
    }

    private static void ThrowIfNotInitialized()
    {
        if (Volatile.Read(ref _state) != FactoryState.Initialized)
            throw new InvalidOperationException(ZVecDefaults.Errors.FactoryNotInitialized);
    }
}
