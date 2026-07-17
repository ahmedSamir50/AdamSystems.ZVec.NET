namespace ZVec.NET;

/// <summary>Schema evolution (DDL) surface for a ZVec collection.</summary>
public interface IZvecCollectionDdl
{
    void AddColumn(ZVecFieldSchema field, string? defaultExpression = null);
    void DropColumn(string columnName);
    void AlterColumn(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null);
    void CreateIndex(string columnName, ZVecIndexParam indexParam);
    void DropIndex(string columnName);
    void Optimize();
    ValueTask AddColumnAsync(ZVecFieldSchema field, string? defaultExpression = null, CancellationToken ct = default);
    ValueTask DropColumnAsync(string columnName, CancellationToken ct = default);
    ValueTask AlterColumnAsync(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null, CancellationToken ct = default);
    ValueTask CreateIndexAsync(string columnName, ZVecIndexParam indexParam, CancellationToken ct = default);
    ValueTask DropIndexAsync(string columnName, CancellationToken ct = default);
    ValueTask OptimizeAsync(CancellationToken ct = default);
}
