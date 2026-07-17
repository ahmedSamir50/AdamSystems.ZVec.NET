namespace ZVec.NET.DependencyInjection;

/// <summary>
/// Options for registering a specific ZVec collection in the Dependency Injection container.
/// </summary>
public sealed class ZVecCollectionRegistrationOptions
{
    /// <summary>
    /// Gets or sets the database path where the collection is stored.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the collection schema. If provided, the collection will be created and opened.
    /// If null, the collection is assumed to exist and will only be opened.
    /// For <c>AddZVecCollection&lt;T&gt;</c>, defaults to schema from the mapped type when unset.
    /// </summary>
    public ZVecCollectionSchema? Schema { get; set; }

    /// <summary>
    /// Gets or sets the collection open options. When null, options are built from
    /// <see cref="EnableMmap"/> and <see cref="ReadOnly"/>.
    /// </summary>
    public ZVecCollectionOptions? Options { get; set; }

    /// <summary>
    /// Convenience mmap flag used when <see cref="Options"/> is null.
    /// </summary>
    public bool EnableMmap { get; set; } = ZVecDefaults.CollectionOptions.EnableMmap;

    /// <summary>
    /// Convenience read-only flag used when <see cref="Options"/> is null.
    /// </summary>
    public bool ReadOnly { get; set; } = ZVecDefaults.CollectionOptions.ReadOnly;

    /// <summary>
    /// When true (default for typed registration), create-and-open using <see cref="Schema"/>
    /// (or schema-from-type). When false, open an existing collection path only.
    /// </summary>
    public bool Create { get; set; } = true;

    internal ZVecCollectionOptions ResolveOptions()
        => Options ?? new ZVecCollectionOptions
        {
            EnableMmap = EnableMmap,
            ReadOnly = ReadOnly
        };
}
