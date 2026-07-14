using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET;

/// <summary>
/// Managed wrapper around a native ZVec collection handle.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dispose vs Destroy semantics:</b><br/>
/// <see cref="Dispose"/>/<see cref="DisposeAsync"/> → <c>zvec_collection_close</c> (data is preserved).<br/>
/// <see cref="Destroy"/>/<see cref="DestroyAsync"/> → <c>zvec_collection_destroy</c> then close (data is deleted).
/// </para>
/// <para>
/// <b>Thread safety:</b> Idempotency and mutual exclusion for all disposal paths are achieved
/// with <see cref="Interlocked.Exchange(ref int, ref int)"/> on <c>int</c> flags — no custom lock.
/// This mirrors the BCL <see cref="System.Runtime.InteropServices.SafeHandle"/> pattern.
/// </para>
/// </remarks>
public sealed class ZVecCollection : IZvecCollection
{
    // Raw native handle — we manage it manually (not via SafeHandle) so we can
    // control close-vs-destroy ordering precisely.
    private readonly nint _handle;

    // 0 = alive, 1 = disposed. Interlocked.Exchange guarantees exactly-once close
    // even under concurrent Dispose + DisposeAsync.
    private int _disposed;

    // 0 = not destroyed, 1 = destroyed. Prevents double-destroy.
    private int _destroyed;

    /// <inheritdoc/>
    public string Path { get; }

    /// <inheritdoc/>
    public ZVecCollectionSchema? Schema { get; }

    // Token cancelled when ZVecFactory.Shutdown() is called.
    private readonly CancellationToken _factoryShutdownToken;

    internal ZVecCollection(nint handle, string path, ZVecCollectionSchema? schema, CancellationToken factoryShutdownToken)
    {
        ArgumentOutOfRangeException.ThrowIfZero(handle, nameof(handle));
        _handle = handle;
        Path = path;
        Schema = schema;
        _factoryShutdownToken = factoryShutdownToken;
    }

    // =========================================================================
    // Lifecycle — Dispose / DisposeAsync
    // =========================================================================

    /// <summary>
    /// Closes the collection. Idempotent — safe to call multiple times and
    /// concurrently with <see cref="DisposeAsync"/>.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return; // Already closed — no-op.

        NativeMethods.zvec_collection_close(_handle);
    }

    /// <summary>
    /// Asynchronously closes the collection. Idempotent.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    // =========================================================================
    // Lifecycle — Destroy / DestroyAsync
    // =========================================================================

    /// <inheritdoc/>
    /// <remarks>
    /// Calls <c>zvec_collection_destroy</c> (deletes on-disk data) first, then
    /// <c>zvec_collection_close</c>. The destroy → close ordering is required by the
    /// native C API contract. Subsequent calls (including <see cref="Dispose"/>) are no-ops.
    /// </remarks>
    public void Destroy()
    {
        // Guard: only one caller can execute the destroy path.
        if (Interlocked.Exchange(ref _destroyed, 1) != 0)
            return; // Already destroyed — no-op.

        // Destroy deletes the on-disk data.
        NativeMethods.zvec_collection_destroy(_handle);

        // Close the handle (no-op if Dispose() already ran concurrently,
        // but _disposed guards against double-close).
        Dispose();
    }

    /// <inheritdoc/>
    public ValueTask DestroyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Destroy();
        return ValueTask.CompletedTask;
    }
}
