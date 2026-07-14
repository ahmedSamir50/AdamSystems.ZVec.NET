namespace AdamSystems.ZVec.NET;

/// <summary>Collection schema: name, scalar fields, vector fields, segment sizing.</summary>
public sealed class ZVecCollectionSchema
{
    /// <summary>The name of the collection.</summary>
    public required string Name { get; init; }

    /// <summary>List of scalar field schemas inside the collection.</summary>
    public IReadOnlyList<ZVecFieldSchema> Fields { get; init; } = [];

    /// <summary>List of vector field schemas inside the collection.</summary>
    public IReadOnlyList<ZVecVectorSchema> Vectors { get; init; } = [];

    /// <summary>Maximum document count allowed per segment before rolling over. Default is 10,000,000.</summary>
    public int MaxDocCountPerSegment { get; init; } = ZVecDefaults.Collection.MaxDocCountPerSegment;
}
