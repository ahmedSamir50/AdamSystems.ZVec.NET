namespace AdamSystems.ZVec.NET;

/// <summary>Scalar field schema.</summary>
public sealed class ZVecFieldSchema
{
    /// <summary>The name of the scalar field.</summary>
    public required string Name { get; init; }

    /// <summary>The data type of the scalar field.</summary>
    public ZVecDataType DataType { get; init; }

    /// <summary>Whether the field is nullable.</summary>
    public bool Nullable { get; init; }

    /// <summary>Optional inverted index parameters associated with the scalar field.</summary>
    public ZVecInvertIndexParam? IndexParam { get; init; }
}
