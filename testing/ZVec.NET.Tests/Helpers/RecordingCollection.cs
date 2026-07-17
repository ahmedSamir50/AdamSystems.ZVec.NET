using ZVec.NET.Query;

namespace ZVec.NET.Tests.Helpers;

/// <summary>
/// Minimal <see cref="IZvecCollection"/> fake that records calls for typed façade unit tests.
/// Dynamic/native coverage remains in the existing E12–E18 suites.
/// </summary>
internal sealed class RecordingCollection : IZvecCollection
{
    public string Path { get; set; } = "/tmp/recording";
    public ZVecCollectionSchema? Schema { get; set; }
    public ZVecCollectionOptions Options { get; set; } = new();
    public ZVecCollectionStats Stats { get; set; } = new();

    public List<ZVecDoc> InsertedDocs { get; } = [];
    public List<ZVecDoc> UpdatedDocs { get; } = [];
    public List<ZVecDoc> UpsertedDocs { get; } = [];
    public List<string> DeletedIds { get; } = [];
    public List<string> DeleteFilters { get; } = [];
    public List<(ZVecQuery Query, int Topk, string? Filter, bool IncludeVector)> Queries { get; } = [];
    public List<ZVecFieldSchema> AddedColumns { get; } = [];
    public List<string> DroppedColumns { get; } = [];
    public List<(string Column, string? NewName)> AlteredColumns { get; } = [];
    public List<(string Column, ZVecIndexParam Param)> CreatedIndexes { get; } = [];
    public List<string> DroppedIndexes { get; } = [];
    public int OptimizeCalls { get; private set; }

    public ZVecDoc? FetchResult { get; set; }
    public IReadOnlyList<ZVecDoc> QueryResult { get; set; } = [];

    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void Destroy() { }
    public ValueTask DestroyAsync(CancellationToken ct = default) => ValueTask.CompletedTask;
    public ZVecCollectionStats GetStats() => Stats;

    public ZVecStatus Insert(ZVecDoc doc)
    {
        InsertedDocs.Add(doc);
        return Ok();
    }

    public ZVecStatus Insert(ReadOnlySpan<ZVecDoc> docs)
    {
        foreach (var d in docs) InsertedDocs.Add(d);
        return Ok();
    }

    public IReadOnlyList<ZVecWriteResult> InsertWithResults(ReadOnlySpan<ZVecDoc> docs)
    {
        Insert(docs);
        return [];
    }

    public ValueTask<ZVecStatus> InsertAsync(ZVecDoc doc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<ZVecStatus>(Insert(doc));
    }

    public ValueTask<IReadOnlyList<ZVecWriteResult>> InsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Insert(docs.ToArray());
        return new ValueTask<IReadOnlyList<ZVecWriteResult>>([]);
    }

    public ZVecStatus Update(ZVecDoc doc)
    {
        UpdatedDocs.Add(doc);
        return Ok();
    }

    public ZVecStatus Update(ReadOnlySpan<ZVecDoc> docs)
    {
        foreach (var d in docs) UpdatedDocs.Add(d);
        return Ok();
    }

    public IReadOnlyList<ZVecWriteResult> UpdateWithResults(ReadOnlySpan<ZVecDoc> docs)
    {
        Update(docs);
        return [];
    }

    public ValueTask<ZVecStatus> UpdateAsync(ZVecDoc doc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<ZVecStatus>(Update(doc));
    }

    public ValueTask<ZVecStatus> UpdateAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Update(docs.ToArray());
        return new ValueTask<ZVecStatus>(Ok());
    }

    public ZVecStatus Upsert(ZVecDoc doc)
    {
        UpsertedDocs.Add(doc);
        return Ok();
    }

    public ZVecStatus Upsert(ReadOnlySpan<ZVecDoc> docs)
    {
        foreach (var d in docs) UpsertedDocs.Add(d);
        return Ok();
    }

    public IReadOnlyList<ZVecWriteResult> UpsertWithResults(ReadOnlySpan<ZVecDoc> docs)
    {
        Upsert(docs);
        return [];
    }

    public ValueTask<ZVecStatus> UpsertAsync(ZVecDoc doc, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<ZVecStatus>(Upsert(doc));
    }

    public ValueTask<ZVecStatus> UpsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Upsert(docs.ToArray());
        return new ValueTask<ZVecStatus>(Ok());
    }

    public ZVecStatus Delete(string pk)
    {
        DeletedIds.Add(pk);
        return Ok();
    }

    public ZVecStatus Delete(ReadOnlySpan<string> pks)
    {
        foreach (var pk in pks) DeletedIds.Add(pk);
        return Ok();
    }

    public IReadOnlyList<ZVecWriteResult> DeleteWithResults(ReadOnlySpan<string> pks)
    {
        Delete(pks);
        return [];
    }

    public ZVecStatus DeleteByFilter(string filter)
    {
        DeleteFilters.Add(filter);
        return Ok();
    }

    public ValueTask<ZVecStatus> DeleteAsync(string pk, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<ZVecStatus>(Delete(pk));
    }

    public ValueTask<ZVecStatus> DeleteAsync(IReadOnlyList<string> pks, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Delete(pks.ToArray());
        return new ValueTask<ZVecStatus>(Ok());
    }

    public ValueTask<ZVecStatus> DeleteByFilterAsync(string filter, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<ZVecStatus>(DeleteByFilter(filter));
    }

    public ZVecDoc? Fetch(string pk, bool includeVector = false) => FetchResult;

    public IReadOnlyList<ZVecDoc> Fetch(ReadOnlySpan<string> pks, bool includeVector = false)
        => FetchResult is null ? [] : [FetchResult];

    public ValueTask<ZVecDoc?> FetchAsync(string pk, bool includeVector = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<ZVecDoc?>(Fetch(pk, includeVector));
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> FetchAsync(IReadOnlyList<string> pks, bool includeVector = false, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<IReadOnlyList<ZVecDoc>>(Fetch(pks.ToArray(), includeVector));
    }

    public IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk = 10, string? filter = null, bool includeVector = true)
    {
        Queries.Add((query, topk, filter, includeVector));
        return QueryResult;
    }

    public IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk, ZVecFilterBuilder filter, bool includeVector = true)
        => Query(query, topk, filter.Build(), includeVector);

    public IReadOnlyList<ZVecDoc> Query(IReadOnlyList<ZVecQuery> queries, int topk = 10, ZVecReranker? reranker = null, string? filter = null, bool includeVector = true)
        => QueryResult;

    public IReadOnlyList<ZVecDoc> Query(IReadOnlyList<ZVecQuery> queries, int topk, ZVecReranker? reranker, ZVecFilterBuilder filter, bool includeVector = true)
        => QueryResult;

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(ZVecQuery query, int topk = 10, string? filter = null, bool includeVector = true, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<IReadOnlyList<ZVecDoc>>(Query(query, topk, filter, includeVector));
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(ZVecQuery query, int topk, ZVecFilterBuilder filter, bool includeVector = true, CancellationToken ct = default)
        => QueryAsync(query, topk, filter.Build(), includeVector, ct);

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(IReadOnlyList<ZVecQuery> queries, int topk = 10, ZVecReranker? reranker = null, string? filter = null, bool includeVector = true, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return new ValueTask<IReadOnlyList<ZVecDoc>>(QueryResult);
    }

    public ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(IReadOnlyList<ZVecQuery> queries, int topk, ZVecReranker? reranker, ZVecFilterBuilder filter, bool includeVector = true, CancellationToken ct = default)
        => QueryAsync(queries, topk, reranker, filter.Build(), includeVector, ct);

#pragma warning disable CS0618
    public IReadOnlyList<ZVecDoc> QueryGroupBy(ZVecGroupByQuery groupQuery) => throw new NotSupportedException();
    public ValueTask<IReadOnlyList<ZVecDoc>> QueryGroupByAsync(ZVecGroupByQuery groupQuery, CancellationToken ct = default)
        => throw new NotSupportedException();
#pragma warning restore CS0618

    public void AddColumn(ZVecFieldSchema field, string? defaultExpression = null) => AddedColumns.Add(field);
    public void DropColumn(string columnName) => DroppedColumns.Add(columnName);
    public void AlterColumn(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null)
        => AlteredColumns.Add((columnName, newName));
    public void CreateIndex(string columnName, ZVecIndexParam indexParam) => CreatedIndexes.Add((columnName, indexParam));
    public void DropIndex(string columnName) => DroppedIndexes.Add(columnName);
    public void Optimize() => OptimizeCalls++;

    public ValueTask AddColumnAsync(ZVecFieldSchema field, string? defaultExpression = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        AddColumn(field, defaultExpression);
        return ValueTask.CompletedTask;
    }

    public ValueTask DropColumnAsync(string columnName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        DropColumn(columnName);
        return ValueTask.CompletedTask;
    }

    public ValueTask AlterColumnAsync(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        AlterColumn(columnName, newName, newSchema);
        return ValueTask.CompletedTask;
    }

    public ValueTask CreateIndexAsync(string columnName, ZVecIndexParam indexParam, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        CreateIndex(columnName, indexParam);
        return ValueTask.CompletedTask;
    }

    public ValueTask DropIndexAsync(string columnName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        DropIndex(columnName);
        return ValueTask.CompletedTask;
    }

    public ValueTask OptimizeAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Optimize();
        return ValueTask.CompletedTask;
    }

    private static ZVecStatus Ok() => new() { Code = ZVecErrorCode.Ok };
}
