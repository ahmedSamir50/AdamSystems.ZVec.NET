namespace ZVec.NET;

/// <summary>Lifecycle surface for a ZVec collection (path, schema, dispose/destroy).</summary>
public interface IZvecCollectionLifecycle : IDisposable, IAsyncDisposable
{
    /// <summary>The file system path where the collection is stored.</summary>
    string Path { get; }

    /// <summary>
    /// The schema configuration of this collection.
    /// May be <c>null</c> when opened without a schema (e.g. via <c>Open</c>).
    /// Updated in-memory after successful DDL that mutates columns.
    /// </summary>
    ZVecCollectionSchema? Schema { get; }

    /// <summary>Options supplied when the collection was opened or created.</summary>
    ZVecCollectionOptions Options { get; }

    /// <summary>
    /// Destroys the collection: deletes all on-disk data, then closes the handle.
    /// Idempotent after a successful destroy. Throws <see cref="ObjectDisposedException"/>
    /// if <see cref="IDisposable.Dispose"/> already closed the handle.
    /// </summary>
    void Destroy();

    /// <summary>Asynchronously destroys the collection (cancellation-aware sync wrapper).</summary>
    ValueTask DestroyAsync(CancellationToken ct = default);
}
