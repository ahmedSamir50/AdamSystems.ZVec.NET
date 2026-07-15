namespace AdamSystems.ZVec.NET.DependencyInjection;

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
    /// </summary>
    public ZVecCollectionSchema? Schema { get; set; }

    /// <summary>
    /// Gets or sets the collection open options.
    /// </summary>
    public ZVecCollectionOptions? Options { get; set; }
}
