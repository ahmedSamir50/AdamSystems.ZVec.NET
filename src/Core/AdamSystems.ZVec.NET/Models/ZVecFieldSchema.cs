namespace AdamSystems.ZVec.NET;

/// <summary>Scalar field schema.</summary>
public sealed class ZVecFieldSchema
{
    public required string Name { get; init; }
    public ZVecDataType DataType { get; init; }
    public bool Nullable { get; init; }
    public ZVecInvertIndexParam? IndexParam { get; init; }
}
