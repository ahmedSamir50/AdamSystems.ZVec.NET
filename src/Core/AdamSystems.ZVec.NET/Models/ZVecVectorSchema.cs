namespace AdamSystems.ZVec.NET;

/// <summary>Vector field schema (dense or sparse).</summary>
public sealed class ZVecVectorSchema
{
    /// <summary>The name of the vector field.</summary>
    public required string Name { get; init; }

    /// <summary>The vector data type (e.g. VectorFp32, SparseVectorFp32).</summary>
    public ZVecDataType DataType { get; init; }

    /// <summary>The dimension of the vector field.</summary>
    public int Dimension { get; init; }

    /// <summary>Optional index parameters associated with the vector field.</summary>
    public ZVecIndexParam? IndexParam { get; init; }
}
