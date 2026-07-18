using System.Collections.Concurrent;
using ZVec.NET.Internal;
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
/// Process-wide native load/init/shutdown is delegated to <see cref="ZVecNativeLifecycle"/>.
/// </para>
/// </remarks>
public sealed class ZVecFactory : IZvecFactory
{
    // =========================================================================
    // State machine — transitions only go forward: Uninitialized → Initialized → ShutDown.
    // =========================================================================
    private int _state = FactoryState.Uninitialized;

    private static class FactoryState
    {
        internal const int Uninitialized = 0;
        internal const int Initialized   = 1;
        internal const int ShutDown      = 2;
    }

    /// <summary>Returns <c>true</c> if the factory has been successfully initialized.</summary>
    public bool IsInitialized =>
        ZVecNativeLifecycle.WithGlobalLock(() => _state == FactoryState.Initialized);

    /// <summary>Returns <c>true</c> if the native library is currently loaded and initialized globally.</summary>
    internal static bool IsNativeLibraryInitialized => ZVecNativeLifecycle.IsNativeLibraryInitialized;

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
        ZVecNativeLifecycle.WithGlobalLock(() =>
        {
            if (_state != FactoryState.Uninitialized)
            {
                return; // Already initialized or shut down — no-op.
            }

            var acquired = false;
            try
            {
                ZVecNativeLifecycle.Acquire(options);
                acquired = true;

                int maxNative = options?.MaxConcurrentNativeCalls
                    ?? ZVecDefaults.GlobalOptions.MaxConcurrentNativeCalls;
                if (maxNative > 0)
                    _nativeCallGate = new SemaphoreSlim(maxNative, maxNative);

                _state = FactoryState.Initialized;
                acquired = false;
            }
            catch
            {
                if (acquired)
                    ZVecNativeLifecycle.Release();
                throw;
            }
        });
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
        ZVecCollection[]? toClose = null;

        ZVecNativeLifecycle.WithGlobalLock(() =>
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
        });

        if (toClose is null)
            return;

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

        ZVecNativeLifecycle.WithGlobalLock(() =>
        {
            if (_state != FactoryState.ShutDown)
            {
                return;
            }

            ZVecNativeLifecycle.Release();

            _nativeCallGate?.Dispose();
            _nativeCallGate = null;

            // State goes back to Uninitialized so it can be re-initialized if needed (especially for tests)
            _shutdownCts = new CancellationTokenSource();
            _state = FactoryState.Uninitialized;
        });
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

        using var nativeSchema = new NativeCollectionSchemaBuilder(schema);
        using var nativeOptions = options != null ? new NativeCollectionOptionsBuilder(options) : null;

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

        using var nativeOptions = options != null ? new NativeCollectionOptionsBuilder(options) : null;

        var rc = NativeMethods.zvec_collection_open(path, nativeOptions?.Handle ?? IntPtr.Zero, out IntPtr handle);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Open));

        // Bind on-disk schema via zvec_collection_get_schema for FieldTypeMap / unmarshalling.
        ZVecCollectionSchema schema;
        try
        {
            schema = NativeSchemaMarshaller.FromOpenCollection(handle);
        }
        catch
        {
            try { NativeMethods.zvec_collection_close(handle); } catch { /* best effort */ }
            throw;
        }

        var collection = new ZVecCollection(handle, path, schema, _shutdownCts.Token, this, options);
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

        if (!NativeLibraryResolver.IsLoaded)
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

    /// <summary>True when <see cref="ZVecOptions.MaxConcurrentNativeCalls"/> created a throttle semaphore.</summary>
    internal bool HasNativeCallGate => _nativeCallGate is not null;

    /// <summary>Acquires the optional native-call throttle (no-op when unlimited).</summary>
    internal void EnterNativeCall(CancellationToken cancellationToken = default)
    {
        _nativeCallGate?.Wait(cancellationToken);
    }

    /// <summary>
    /// Asynchronously acquires the optional native-call throttle (no-op when unlimited).
    /// Honors <paramref name="cancellationToken"/> while waiting; mid-P/Invoke cancel is unaffected.
    /// </summary>
    internal ValueTask EnterNativeCallAsync(CancellationToken cancellationToken = default)
    {
        if (_nativeCallGate is null)
            return ValueTask.CompletedTask;

        return new ValueTask(_nativeCallGate.WaitAsync(cancellationToken));
    }

    /// <summary>Releases the optional native-call throttle (no-op when unlimited).</summary>
    internal void ExitNativeCall()
    {
        _nativeCallGate?.Release();
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
