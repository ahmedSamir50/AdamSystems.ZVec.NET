namespace ZVec.NET;

/// <summary>
/// A fluent builder for creating <see cref="ZVecCollectionSchema"/> instances.
/// </summary>
public sealed class ZVecCollectionSchemaBuilder
{
    private readonly string _name;
    private int _maxDocCountPerSegment = ZVecDefaults.Collection.MaxDocCountPerSegment;
    private readonly List<ZVecFieldSchema> _fields = [];
    private readonly List<ZVecVectorSchema> _vectors = [];

    /// <summary>
    /// Initializes a new builder for the specified collection name.
    /// </summary>
    /// <param name="name">The collection name. Must not be null, empty, or whitespace.</param>
    public ZVecCollectionSchemaBuilder(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
    }

    /// <summary>
    /// Adds an existing scalar field schema.
    /// </summary>
    public ZVecCollectionSchemaBuilder AddField(ZVecFieldSchema field)
    {
        ArgumentNullException.ThrowIfNull(field);
        _fields.Add(field);
        return this;
    }

    /// <summary>
    /// Adds a scalar field to the collection schema.
    /// </summary>
    public ZVecCollectionSchemaBuilder AddField(
        string name,
        ZVecDataType dataType,
        bool nullable = false,
        ZVecInvertIndexParam? index = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _fields.Add(new ZVecFieldSchema
        {
            Name = name,
            DataType = dataType,
            Nullable = nullable,
            IndexParam = index
        });
        return this;
    }

    /// <summary>
    /// Adds an existing vector field schema.
    /// </summary>
    public ZVecCollectionSchemaBuilder AddVector(ZVecVectorSchema vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        _vectors.Add(vector);
        return this;
    }

    /// <summary>
    /// Adds a dense or sparse vector field to the collection schema.
    /// </summary>
    public ZVecCollectionSchemaBuilder AddVector(
        string name,
        ZVecDataType dataType,
        int dimension,
        ZVecIndexParam? index = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (dimension <= 0)
            throw new ArgumentException(ZVecDefaults.Errors.VectorDimensionMustBePositive, nameof(dimension));

        _vectors.Add(new ZVecVectorSchema
        {
            Name = name,
            DataType = dataType,
            Dimension = dimension,
            IndexParam = index
        });
        return this;
    }

    /// <summary>
    /// Overrides the default maximum document count per segment.
    /// </summary>
    public ZVecCollectionSchemaBuilder WithMaxDocCountPerSegment(int value)
    {
        if (value <= 0)
            throw new ArgumentException(ZVecDefaults.Errors.MaxDocCountPerSegmentMustBePositive, nameof(value));

        _maxDocCountPerSegment = value;
        return this;
    }

    /// <summary>
    /// Constructs and returns the final <see cref="ZVecCollectionSchema"/>.
    /// </summary>
    public ZVecCollectionSchema Build()
    {
        return new ZVecCollectionSchema
        {
            Name = _name,
            MaxDocCountPerSegment = _maxDocCountPerSegment,
            Fields = _fields.ToArray(),
            Vectors = _vectors.ToArray()
        };
    }
}
