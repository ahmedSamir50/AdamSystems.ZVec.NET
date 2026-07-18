using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// Detects whether a real ZVec native library is loadable. Integration/memory tests call
/// <see cref="SkipIfNotAvailable"/> so CI without a RID binary skips rather than fails.
/// </summary>
/// <remarks>
/// Detection is process-wide and serialized. Parallel <see cref="IClassFixture{T}"/> instances
/// previously raced Initialize/Shutdown and could call <c>zvec_shutdown</c> while another class
/// was still constructing — heap corruption (<c>0xC0000374</c>) on Pack CI.
/// </remarks>
public class ZVecRealNativeFixture : IDisposable
{
    private static readonly object Gate = new();
    private static int s_liveFixtures;
    private static bool s_detectDone;
    private static bool s_available;
    private static string? s_failReason;
    private static ZVecFactory? s_sharedFactory;

    public bool IsRealNativeAvailable { get; private set; }

    public ZVecRealNativeFixture()
    {
        lock (Gate)
        {
            s_liveFixtures++;
            if (!s_detectDone)
            {
                s_available = DetectOnce();
                s_detectDone = true;
            }

            IsRealNativeAvailable = s_available;
        }
    }

    private static bool DetectOnce()
    {
        var testPath = Path.Combine(Path.GetTempPath(), $"zvec_real_detect_{Guid.NewGuid():N}");

        try
        {
            NativeLibraryResolver.UseRealLibrary();
            NativeLibraryResolver.EnsureLoaded();

            var version = NativeMethods.GetVersionString();
            if (string.IsNullOrEmpty(version))
            {
                s_failReason = "native GetVersionString returned empty";
                return false;
            }

            s_sharedFactory = new ZVecFactory();
            s_sharedFactory.Initialize();

            // Flat index: fastest/most portable probe (HNSW previously failed detection on CI).
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
                        IndexParam = new ZVecFlatIndexParam()
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
                col = s_sharedFactory.CreateAndOpen(testPath, schema);

                bool pathExists = Directory.Exists(testPath) || File.Exists(testPath);
                if (!pathExists)
                {
                    s_failReason = $"CreateAndOpen succeeded but path missing: {testPath}";
                    ShutdownSharedUnlocked();
                    return false;
                }

                var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
                var doc = ZVecDoc.Create("doc1",
                    denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
                    fields: new Dictionary<string, object> { ["name"] = "Alice", ["age"] = 30 });
                col.Insert(doc);
                return true;
            }
            finally
            {
                try { col?.Destroy(); } catch { /* probe cleanup */ }
            }
        }
        catch (DllNotFoundException ex)
        {
            s_failReason = ex.Message;
            ShutdownSharedUnlocked();
            return false;
        }
        catch (EntryPointNotFoundException ex)
        {
            s_failReason = ex.Message;
            ShutdownSharedUnlocked();
            return false;
        }
        catch (Exception ex)
        {
            s_failReason = $"{ex.GetType().Name}: {ex.Message}";
            ShutdownSharedUnlocked();
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(testPath))
                    Directory.Delete(testPath, recursive: true);
                else if (File.Exists(testPath))
                    File.Delete(testPath);
            }
            catch
            {
                // Ignore cleanup failures during detection.
            }
        }
    }

    public void SkipIfNotAvailable()
    {
        if (!IsRealNativeAvailable)
        {
            Assert.Skip(
                "Real ZVec native library is not available. Skipping integration test."
                + (string.IsNullOrEmpty(s_failReason) ? "" : $" Reason: {s_failReason}"));
        }
    }

    /// <summary>
    /// Releases the shared native session so a test can re-run <see cref="ZVecFactory.Initialize"/>
    /// (e.g. ABI version gate). Call <see cref="ResumeNativeSession"/> in <c>finally</c>.
    /// </summary>
    public void SuspendNativeSession()
    {
        SkipIfNotAvailable();
        lock (Gate)
        {
            ShutdownSharedUnlocked();
        }
    }

    /// <summary>Re-initializes native after <see cref="SuspendNativeSession"/>.</summary>
    public void ResumeNativeSession()
    {
        if (!IsRealNativeAvailable)
            return;

        lock (Gate)
        {
            if (s_sharedFactory is { IsInitialized: true })
                return;

            s_sharedFactory ??= new ZVecFactory();
            s_sharedFactory.Initialize();
        }
    }

    public void Dispose()
    {
        lock (Gate)
        {
            s_liveFixtures--;
            if (s_liveFixtures > 0)
                return;

            s_liveFixtures = 0;
            // When unavailable, ensure any half-init factory is torn down.
            // When available, leave the process-wide native singleton up — parallel unit tests
            // that call Initialize share _globalNativeInitCount; fixture Dispose must not
            // zvec_shutdown underneath them.
            if (!s_available)
                ShutdownSharedUnlocked();
        }
    }

    private static void ShutdownSharedUnlocked()
    {
        try
        {
            s_sharedFactory?.Shutdown();
        }
        catch
        {
            // Ignore shutdown failures during fixture teardown.
        }
        finally
        {
            s_sharedFactory = null;
        }
    }
}
