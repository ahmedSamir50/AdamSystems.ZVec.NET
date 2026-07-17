namespace ZVec.NET;

/// <summary>
/// Defines the process-wide factory for initializing ZVec and opening vector collections.
/// </summary>
public interface IZvecFactory : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Returns true if this factory instance has been successfully initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes the ZVec process-wide native state (first-init-wins).
    /// Subsequent calls from any thread are no-ops.
    /// </summary>
    void Initialize(ZVecOptions? options = null);

    /// <summary>
    /// Asynchronously initializes the ZVec process-wide native state.
    /// </summary>
    ValueTask InitializeAsync(ZVecOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Shuts down the process-wide native state (idempotent).
    /// </summary>
    void Shutdown();

    /// <summary>
    /// Asynchronously shuts down the process-wide native state.
    /// </summary>
    ValueTask ShutdownAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a new collection and opens it.
    /// </summary>
    IZvecCollection CreateAndOpen(string path, ZVecCollectionSchema schema, ZVecCollectionOptions? options = null);

    /// <summary>
    /// Asynchronously creates a new collection and opens it.
    /// </summary>
    ValueTask<IZvecCollection> CreateAndOpenAsync(string path, ZVecCollectionSchema schema, ZVecCollectionOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Opens an existing collection.
    /// </summary>
    IZvecCollection Open(string path, ZVecCollectionOptions? options = null);

    /// <summary>
    /// Asynchronously opens an existing collection.
    /// </summary>
    ValueTask<IZvecCollection> OpenAsync(string path, ZVecCollectionOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Returns the native library version string (for example <c>0.5.1</c>).
    /// Requires the factory to be initialized and the native library to be loaded.
    /// </summary>
    string GetNativeVersion();

    /// <summary>
    /// Returns required ABI metadata and, when the native library is loaded, the discovered version.
    /// </summary>
    ZVecNativeAbiInfo GetAbiInfo();
}
