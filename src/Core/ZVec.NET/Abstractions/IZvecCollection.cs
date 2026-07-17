namespace ZVec.NET;

/// <summary>
/// Defines a ZVec vector collection — lifecycle, CRUD, query, and DDL operations.
/// </summary>
/// <remarks>
/// <para>
/// Dispose/DisposeAsync perform a <c>zvec_collection_close</c> (safe to call multiple times;
/// internally idempotent). <see cref="IZvecCollectionLifecycle.Destroy"/> first calls
/// <c>zvec_collection_destroy</c> (deletes on-disk data) then close. Destroy after Dispose
/// throws <see cref="ObjectDisposedException"/>.
/// </para>
/// <para>
/// Role interfaces (<see cref="IZvecCollectionLifecycle"/>, <see cref="IZvecCollectionWrites"/>,
/// <see cref="IZvecCollectionQueries"/>, <see cref="IZvecCollectionDdl"/>) allow depending on
/// a narrower surface; this composite remains the primary registration type.
/// </para>
/// <para>
/// Async members are cancellation-aware wrappers around synchronous native P/Invoke calls.
/// </para>
/// </remarks>
public interface IZvecCollection :
    IZvecCollectionLifecycle,
    IZvecCollectionWrites,
    IZvecCollectionQueries,
    IZvecCollectionDdl
{
}
