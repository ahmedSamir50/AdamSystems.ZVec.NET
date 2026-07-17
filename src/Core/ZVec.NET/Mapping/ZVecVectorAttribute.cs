namespace ZVec.NET.Mapping;

/// <summary>
/// Marks a dense or sparse vector property and supplies index/dimension metadata required for schema generation.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ZVecVectorAttribute : Attribute
{
    /// <summary>Creates a vector attribute with the required dimension.</summary>
    /// <param name="dimension">Vector dimension (must be positive).</param>
    public ZVecVectorAttribute(int dimension)
    {
        if (dimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(dimension), ZVecDefaults.Errors.VectorDimensionMustBePositive);
        Dimension = dimension;
    }

    /// <summary>Creates a vector attribute with an explicit storage name and dimension.</summary>
    public ZVecVectorAttribute(string name, int dimension)
        : this(dimension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Optional native storage name. When null, the CLR property name is used.</summary>
    public string? Name { get; }

    /// <summary>Vector dimension.</summary>
    public int Dimension { get; }

    /// <summary>Vector data type. Default is <see cref="ZVecDataType.VectorFp32"/>.</summary>
    public ZVecDataType DataType { get; set; } = ZVecDataType.VectorFp32;

    /// <summary>Distance metric for the vector index. Default is Cosine.</summary>
    public ZVecMetricType Metric { get; set; } = ZVecMetricType.Cosine;

    /// <summary>Index family. Default is HNSW.</summary>
    public ZVecIndexType Index { get; set; } = ZVecIndexType.Hnsw;

    /// <summary>HNSW M (ignored for non-HNSW indexes). Default follows <see cref="ZVecDefaults.Hnsw.M"/>.</summary>
    public int M { get; set; } = ZVecDefaults.Hnsw.M;

    /// <summary>HNSW efConstruction (ignored for non-HNSW indexes).</summary>
    public int EfConstruction { get; set; } = ZVecDefaults.Hnsw.EfConstruction;
}
