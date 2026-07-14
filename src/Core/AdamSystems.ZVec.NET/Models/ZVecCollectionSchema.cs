namespace AdamSystems.ZVec.NET;

/// <summary>Collection schema: name, scalar fields, vector fields, segment sizing.</summary>
public sealed class ZVecCollectionSchema
{
    public required string Name { get; init; }
    public IReadOnlyList<ZVecFieldSchema> Fields { get; init; } = [];
    public IReadOnlyList<ZVecVectorSchema> Vectors { get; init; } = [];
    public int MaxDocCountPerSegment { get; init; } = ZVecDefaults.Collection.MaxDocCountPerSegment;
}
