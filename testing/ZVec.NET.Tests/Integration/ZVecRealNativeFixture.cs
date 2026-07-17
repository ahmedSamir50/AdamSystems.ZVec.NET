using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// Detects whether a real ZVec native library is loadable. Integration/memory tests call
/// <see cref="SkipIfNotAvailable"/> so CI without a RID binary skips rather than fails.
/// </summary>
public class ZVecRealNativeFixture : IDisposable
{
    public bool IsRealNativeAvailable { get; private set; }
    private readonly string _testPath;
    private ZVecFactory? _factory;

    public ZVecRealNativeFixture()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_real_detect_{Guid.NewGuid():N}");

        try
        {
            NativeLibraryResolver.UseRealLibrary();
            NativeLibraryResolver.EnsureLoaded();

            var version = NativeMethods.GetVersionString();
            if (string.IsNullOrEmpty(version))
            {
                IsRealNativeAvailable = false;
                return;
            }

            _factory = new ZVecFactory();
            _factory.Initialize();

            var schema = new ZVecCollectionSchema
            {
                Name = "detect_schema",
                Vectors =
                [
                    new ZVecVectorSchema
                    {
                        Name = "embedding",
                        DataType = ZVecDataType.VectorFp32,
                        Dimension = 4,
                        IndexParam = new ZVecHnswIndexParam()
                    }
                ],
                Fields =
                [
                    new ZVecFieldSchema { Name = "name", DataType = ZVecDataType.String },
                    new ZVecFieldSchema { Name = "age", DataType = ZVecDataType.Int32 }
                ]
            };

            IZvecCollection? col = null;
            try
            {
                col = _factory.CreateAndOpen(_testPath, schema);

                bool pathExists = Directory.Exists(_testPath) || File.Exists(_testPath);
                if (!pathExists)
                {
                    IsRealNativeAvailable = false;
                    return;
                }

                var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
                var doc = ZVecDoc.Create("doc1",
                    denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
                    fields: new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30 });
                col.Insert(doc);
                IsRealNativeAvailable = true;
            }
            finally
            {
                col?.Destroy();
            }
        }
        catch (DllNotFoundException)
        {
            IsRealNativeAvailable = false;
        }
        catch (EntryPointNotFoundException)
        {
            IsRealNativeAvailable = false;
        }
        catch (Exception)
        {
            IsRealNativeAvailable = false;
        }
        finally
        {
            Cleanup();
        }
    }

    public void SkipIfNotAvailable()
    {
        if (!IsRealNativeAvailable)
        {
            Assert.Skip("Real ZVec native library is not available. Skipping integration test.");
        }
    }

    private void Cleanup()
    {
        try
        {
            if (Directory.Exists(_testPath))
                Directory.Delete(_testPath, recursive: true);
            else if (File.Exists(_testPath))
                File.Delete(_testPath);
        }
        catch
        {
            // Ignore cleanup failures during detection.
        }
    }

    public void Dispose()
    {
        Cleanup();

        try
        {
            _factory?.Shutdown();
        }
        catch
        {
            // Ignore shutdown failures during fixture teardown.
        }
    }
}
