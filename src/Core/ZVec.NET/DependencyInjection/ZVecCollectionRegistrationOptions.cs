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
    /// Gets or sets the collection schema. Required for <see cref="ZVecCollectionOpenMode.CreateOnly"/>
    /// and <see cref="ZVecCollectionOpenMode.OpenOrCreate"/>. For <c>AddZVecCollection&lt;T&gt;</c>,
    /// defaults to schema from the mapped type when unset.
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
    /// How to open the path. Default <see cref="ZVecCollectionOpenMode.OpenOrCreate"/> is restart-safe.
    /// </summary>
    public ZVecCollectionOpenMode OpenMode { get; set; } = ZVecCollectionOpenMode.OpenOrCreate;

    /// <summary>
    /// Obsolete shim: <see langword="true"/> maps to <see cref="ZVecCollectionOpenMode.CreateOnly"/>;
    /// <see langword="false"/> maps to <see cref="ZVecCollectionOpenMode.OpenOnly"/>.
    /// Prefer <see cref="OpenMode"/> (default <see cref="ZVecCollectionOpenMode.OpenOrCreate"/>).
    /// </summary>
    [Obsolete("Use OpenMode instead. true → CreateOnly, false → OpenOnly. Default OpenMode is OpenOrCreate.")]
    public bool Create
    {
        get => OpenMode == ZVecCollectionOpenMode.CreateOnly;
        set => OpenMode = value
            ? ZVecCollectionOpenMode.CreateOnly
            : ZVecCollectionOpenMode.OpenOnly;
    }

    internal ZVecCollectionOptions ResolveOptions()
        => Options ?? new ZVecCollectionOptions
        {
            EnableMmap = EnableMmap,
            ReadOnly = ReadOnly
        };
}
