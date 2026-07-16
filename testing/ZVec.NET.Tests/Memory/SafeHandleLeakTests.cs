using ZVec.NET.Interop;
using FluentAssertions;

namespace ZVec.NET.Tests.Memory;

public class SafeHandleLeakTests
{
    [Fact]
    public void SafeZvecHandle_Leak_Finalizer_Diagnostic_Warning()
    {
        // Redirect Console.Error to capture the warning output
        using var sw = new StringWriter();
        var originalError = Console.Error;
        Console.SetError(sw);

        try
        {
            // Run in a separate scope so the handle can be collected
            ExecuteLeakAction();

            // Force GC collection and wait for finalizers to run
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Verify warning output was printed to Console.Error
            var output = sw.ToString();
            output.Should().Contain("[Warning] SafeHandle of type 'SafeZvecHandle'");
            output.Should().Contain("was finalized without being explicitly disposed");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ExecuteLeakAction()
    {
        // Instantiate a fake SafeZvecHandle that owns a handle.
        // We pass a dummy pointer (0x12345).
        var handle = new SafeZvecHandle(new IntPtr(0x12345), ownsHandle: true);
        handle.IsInvalid.Should().BeFalse();
        // Allow it to go out of scope without calling Dispose()
    }
}
