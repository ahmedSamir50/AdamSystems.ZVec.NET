using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET.Tests.Integration;

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
            // Ensure mock is disabled, then probe via cached handle path.
            NativeLibraryResolver.UseRealLibrary();
            NativeLibraryResolver.EnsureLoaded();

            // THEN: Try to get version — simplest check
            var version = NativeMethods.GetVersionString();
            IsRealNativeAvailable = !string.IsNullOrEmpty(version) 
                                   && !version.Contains("mock", StringComparison.OrdinalIgnoreCase);

            if (!IsRealNativeAvailable)
            {
                Console.WriteLine("[ZVecRealNativeFixture] Mock library detected via version string");
                return;
            }

            // SECOND: Verify with actual collection creation
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
                
                // ✅ CORRECT: Check WHILE collection is open
                bool pathExists = Directory.Exists(_testPath) || File.Exists(_testPath);
                
                // ✅ Try to insert a document to verify it's functional
                if (pathExists)
                {
                    try
                    {
                         var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
                        var doc = ZVecDoc.Create("doc1",
                            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
                            fields: new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30 });
                        col.Insert(doc);
                        IsRealNativeAvailable = true;
                        Console.WriteLine("[ZVecRealNativeFixture] Real native library verified via insert operation");
                    }
                    catch (Exception ex)
                    {
                        IsRealNativeAvailable = false;
                        Console.WriteLine($"[ZVecRealNativeFixture] Insert failed: {ex.Message}");
                    }
                }
                else
                {
                    IsRealNativeAvailable = false;
                    Console.WriteLine("[ZVecRealNativeFixture] Path does not exist - likely mock library");
                }
            }
            finally
            {
                // ✅ Proper cleanup order
                col?.Destroy(); // Deletes data and closes
            }
        }
        catch (DllNotFoundException ex)
        {
            IsRealNativeAvailable = false;
            Console.WriteLine($"[ZVecRealNativeFixture] DLL not found: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            IsRealNativeAvailable = false;
            Console.WriteLine($"[ZVecRealNativeFixture] Entry point not found: {ex.Message}");
        }
        catch (Exception ex)
        {
            IsRealNativeAvailable = false;
            Console.WriteLine($"[ZVecRealNativeFixture] Detection failed: {ex.GetType().Name}: {ex.Message}");
            // ✅ DON'T THROW - allow tests to skip gracefully
        }
        finally
        {
            // Final cleanup
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
            {
                Directory.Delete(_testPath, recursive: true);
            }
            else if (File.Exists(_testPath))
            {
                File.Delete(_testPath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZVecRealNativeFixture] Cleanup warning: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Cleanup();
        
        try
        {
            _factory?.Shutdown();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ZVecRealNativeFixture] Shutdown warning: {ex.Message}");
        }
    }
}
