namespace ZVec.NET;

/// <summary>Write / delete surface for a ZVec collection.</summary>
public interface IZvecCollectionWrites
{
    ZVecStatus Insert(ZVecDoc doc);
    ZVecStatus Insert(ReadOnlySpan<ZVecDoc> docs);
    IReadOnlyList<ZVecWriteResult> InsertWithResults(ReadOnlySpan<ZVecDoc> docs);
    ValueTask<ZVecStatus> InsertAsync(ZVecDoc doc, CancellationToken ct = default);
    ValueTask<IReadOnlyList<ZVecWriteResult>> InsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default);

    ZVecStatus Update(ZVecDoc doc);
    ZVecStatus Update(ReadOnlySpan<ZVecDoc> docs);
    IReadOnlyList<ZVecWriteResult> UpdateWithResults(ReadOnlySpan<ZVecDoc> docs);
    ValueTask<ZVecStatus> UpdateAsync(ZVecDoc doc, CancellationToken ct = default);
    ValueTask<ZVecStatus> UpdateAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default);

    ZVecStatus Upsert(ZVecDoc doc);
    ZVecStatus Upsert(ReadOnlySpan<ZVecDoc> docs);
    IReadOnlyList<ZVecWriteResult> UpsertWithResults(ReadOnlySpan<ZVecDoc> docs);
    ValueTask<ZVecStatus> UpsertAsync(ZVecDoc doc, CancellationToken ct = default);
    ValueTask<ZVecStatus> UpsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken ct = default);

    ZVecStatus Delete(string pk);
    ZVecStatus Delete(ReadOnlySpan<string> pks);
    IReadOnlyList<ZVecWriteResult> DeleteWithResults(ReadOnlySpan<string> pks);
    ZVecStatus DeleteByFilter(string filter);
    ValueTask<ZVecStatus> DeleteAsync(string pk, CancellationToken ct = default);
    ValueTask<ZVecStatus> DeleteAsync(IReadOnlyList<string> pks, CancellationToken ct = default);
    ValueTask<ZVecStatus> DeleteByFilterAsync(string filter, CancellationToken ct = default);
}
