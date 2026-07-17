using System.Linq.Expressions;

namespace ZVec.NET;

/// <summary>
/// Typed façade over <see cref="IZvecCollection"/> for a mapped document type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">Concrete document class mapped via <c>ZVec.NET.Mapping</c>.</typeparam>
public interface IZvecCollection<T> : IDisposable, IAsyncDisposable where T : class
{
    /// <summary>Underlying untyped collection (escape hatch).</summary>
    IZvecCollection Untyped { get; }

    /// <inheritdoc cref="IZvecCollectionLifecycle.Path"/>
    string Path { get; }

    /// <inheritdoc cref="IZvecCollectionLifecycle.Schema"/>
    ZVecCollectionSchema? Schema { get; }

    /// <inheritdoc cref="IZvecCollectionLifecycle.Options"/>
    ZVecCollectionOptions Options { get; }

    /// <inheritdoc cref="IZvecCollectionQueries.Stats"/>
    ZVecCollectionStats Stats { get; }

    /// <inheritdoc cref="IZvecCollectionLifecycle.Destroy"/>
    void Destroy();

    /// <inheritdoc cref="IZvecCollectionLifecycle.DestroyAsync"/>
    ValueTask DestroyAsync(CancellationToken ct = default);

    ZVecStatus Insert(T record);
    ZVecStatus Insert(IReadOnlyList<T> records);
    ValueTask<ZVecStatus> InsertAsync(T record, CancellationToken ct = default);
    ValueTask<ZVecStatus> InsertAsync(IReadOnlyList<T> records, CancellationToken ct = default);

    ZVecStatus Update(T record);
    ValueTask<ZVecStatus> UpdateAsync(T record, CancellationToken ct = default);

    ZVecStatus Upsert(T record);
    ValueTask<ZVecStatus> UpsertAsync(T record, CancellationToken ct = default);

    ZVecStatus Delete(string id);
    ZVecStatus DeleteByFilter(Expression<Func<T, bool>> filter);
    ValueTask<ZVecStatus> DeleteAsync(string id, CancellationToken ct = default);
    ValueTask<ZVecStatus> DeleteByFilterAsync(Expression<Func<T, bool>> filter, CancellationToken ct = default);

    T? Fetch(string id, bool includeVector = false);
    ValueTask<T?> FetchAsync(string id, bool includeVector = false, CancellationToken ct = default);

    IReadOnlyList<ZVecHit<T>> Query(
        Expression<Func<T, ReadOnlyMemory<float>>> vectorProperty,
        ReadOnlyMemory<float> queryVector,
        int topK = 10,
        Expression<Func<T, bool>>? filter = null,
        bool includeVector = true);

    ValueTask<IReadOnlyList<ZVecHit<T>>> QueryAsync(
        Expression<Func<T, ReadOnlyMemory<float>>> vectorProperty,
        ReadOnlyMemory<float> queryVector,
        int topK = 10,
        Expression<Func<T, bool>>? filter = null,
        bool includeVector = true,
        CancellationToken ct = default);

    void AddColumn<TProp>(Expression<Func<T, TProp>> property, string? defaultExpression = null);
    ValueTask AddColumnAsync<TProp>(Expression<Func<T, TProp>> property, string? defaultExpression = null, CancellationToken ct = default);

    void DropColumn<TProp>(Expression<Func<T, TProp>> property);
    ValueTask DropColumnAsync<TProp>(Expression<Func<T, TProp>> property, CancellationToken ct = default);

    void AlterColumn<TProp>(Expression<Func<T, TProp>> property, string? newName = null, ZVecFieldSchema? newSchema = null);
    ValueTask AlterColumnAsync<TProp>(Expression<Func<T, TProp>> property, string? newName = null, ZVecFieldSchema? newSchema = null, CancellationToken ct = default);

    void CreateIndex<TProp>(Expression<Func<T, TProp>> property, ZVecIndexParam indexParam);
    ValueTask CreateIndexAsync<TProp>(Expression<Func<T, TProp>> property, ZVecIndexParam indexParam, CancellationToken ct = default);

    void DropIndex<TProp>(Expression<Func<T, TProp>> property);
    ValueTask DropIndexAsync<TProp>(Expression<Func<T, TProp>> property, CancellationToken ct = default);

    /// <summary>
    /// Adds scalar columns present on <typeparamref name="T"/> but missing from the live schema.
    /// Never drops or alters columns. Vector columns cannot be added this way.
    /// </summary>
    /// <remarks>
    /// Native <c>add_column</c> accepts nullable basic numeric types only; added columns are forced
    /// nullable with a numeric default expression. Put string/array fields in the create-time schema.
    /// </remarks>
    void EnsureSchema();

    /// <inheritdoc cref="EnsureSchema"/>
    ValueTask EnsureSchemaAsync(CancellationToken ct = default);

    void Optimize();
    ValueTask OptimizeAsync(CancellationToken ct = default);
}
