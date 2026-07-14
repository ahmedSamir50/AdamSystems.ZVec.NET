namespace AdamSystems.ZVec.NET;

/// <summary>Vector field schema (dense or sparse).</summary>
public sealed class ZVecVectorSchema
{
    public required string Name { get; init; }
    public ZVecDataType DataType { get; init; }
    public int Dimension { get; init; }
    public ZVecIndexParam? IndexParam { get; init; }
}
