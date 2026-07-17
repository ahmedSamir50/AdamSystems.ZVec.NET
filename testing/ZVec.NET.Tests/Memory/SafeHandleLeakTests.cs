using FluentAssertions;
using ZVec.NET.Interop;
using ZVec.NET.Tests.Integration;

namespace ZVec.NET.Tests.Memory;

public class SafeHandleLeakTests : IClassFixture<ZVecRealNativeFixture>
{
    private readonly ZVecRealNativeFixture _fixture;

    public SafeHandleLeakTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void SafeZvecHandle_Leak_Finalizer_Diagnostic_Warning()
    {
        using var sw = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(sw);

        try
        {
            ExecuteSafeHandleLeakAction();

            GC.Collect();
            GC.WaitForPendingFinalizers();

            var output = sw.ToString();
            output.Should().Contain("[Warning] SafeHandle of type 'SafeZvecHandle'");
            output.Should().Contain("was finalized without being explicitly disposed");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void Collection_WithoutDispose_IsCollectedByGc()
    {
        _fixture.SkipIfNotAvailable();

        var testPath = Path.Combine(Path.GetTempPath(), $"zvec_leak_{Guid.NewGuid():N}");
        WeakReference? weakRef = null;

        try
        {
            new Action(() =>
            {
                var factory = new ZVecFactory();
                factory.Initialize();
                var schema = new ZVecCollectionSchema
                {
                    Name = "leak_test",
                    Vectors =
                    [
                        new ZVecVectorSchema
                        {
                            Name = "embedding",
                            DataType = ZVecDataType.VectorFp32,
                            Dimension = 4,
                            IndexParam = new ZVecFlatIndexParam()
                        }
                    ]
                };
                var col = factory.CreateAndOpen(testPath, schema);
                weakRef = new WeakReference(col, trackResurrection: true);
                factory.Shutdown();
            })();

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            weakRef.Should().NotBeNull();
            weakRef!.IsAlive.Should().BeFalse(
                "managed ZVecCollection is collectable without Dispose once the factory releases tracking");
        }
        finally
        {
            try
            {
                if (Directory.Exists(testPath))
                    Directory.Delete(testPath, true);
            }
            catch
            {
                // Ignore cleanup failures
            }
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void ExecuteSafeHandleLeakAction()
    {
        var handle = new SafeZvecHandle(new IntPtr(0x12345), ownsHandle: true);
        handle.IsInvalid.Should().BeFalse();
    }
}
