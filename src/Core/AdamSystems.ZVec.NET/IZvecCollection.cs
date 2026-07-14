namespace AdamSystems.ZVec.NET;

/// <summary>
/// Defines a ZVec vector collection — lifecycle, CRUD, query, and DDL operations.
/// </summary>
/// <remarks>
/// Dispose/DisposeAsync perform a <c>zvec_collection_close</c> (safe to call multiple times;
/// internally idempotent). <see cref="Destroy"/> and <see cref="DestroyAsync"/> first call
/// <c>zvec_collection_destroy</c> (deletes on-disk data) then close. All idempotency and
/// mutual exclusion is achieved via <see cref="System.Threading.Interlocked"/> — no custom locks.
/// </remarks>
public interface IZvecCollection : IDisposable, IAsyncDisposable
{
    /// <summary>The file system path where the collection is stored.</summary>
    string Path { get; }

    /// <summary>
    /// The schema configuration of this collection.
    /// May be <c>null</c> when opened without a schema (e.g. via <c>Open</c>).
    /// </summary>
    ZVecCollectionSchema? Schema { get; }

    /// <summary>
    /// Destroys the collection: deletes all on-disk data, then closes the handle.
    /// Idempotent — subsequent calls are no-ops.
    /// </summary>
    void Destroy();

    /// <summary>
    /// Asynchronously destroys the collection.
    /// </summary>
    ValueTask DestroyAsync(CancellationToken ct = default);
}
