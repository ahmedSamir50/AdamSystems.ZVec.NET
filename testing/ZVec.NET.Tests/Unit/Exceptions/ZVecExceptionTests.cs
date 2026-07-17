using ZVec.NET.Exceptions;
using ZVec.NET.Interop;
using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Exceptions;

[Collection("ResolverStateTests")]
public class ZVecExceptionTests
{
    [Fact]
    public void ZVecAbiMismatchException_ContainsVersionInfo()
    {
        // US-E6.2: Verify version mismatch serialization
        var ex = new ZVecAbiMismatchException(expectedMinimum: "1.2.0", requiredMajor: 1, found: "1.3.0");
        ex.ExpectedVersion.Should().Be("1.2.0");
        ex.FoundVersion.Should().Be("1.3.0");
        ex.Message.Should().Contain("1.2.0").And.Contain("1.3.0").And.Contain("major == 1");
    }

    [Fact]
    public void ZVecErrorCode_Ok_DoesNotThrow()
    {
        // US-E6.3: Ok code does not throw exceptions
        var act = () => ZVecError.ThrowIfFailed(ZVecErrorCode.Ok, "testContext");
        act.Should().NotThrow();
    }

    [Fact]
    public void ZVecErrorCode_Error_ThrowsZVecNativeException_WithDefensiveFallback()
    {
        if (NativeLibraryResolver.IsLoaded) return; // Skip test: Library already loaded in this process.
        // US-E6.3: Error code triggers Native Exception and hits defensive DllNotFound catch
        NativeLibraryResolver.SetMockLibrary("non_existent_mock_library_file_path_abc.dll");
        try
        {
            var act = () => ZVecError.ThrowIfFailed(ZVecErrorCode.InvalidArgument, "CreateCollection");

            var ex = act.Should().Throw<ZVecNativeException>().Which;
            ex.ErrorCode.Should().Be(ZVecErrorCode.InvalidArgument);
            ex.OperationContext.Should().Be("CreateCollection");
            ex.NativeErrorMessage.Should().Be(ZVecError.LibraryNotLoadedFallback);
        }
        finally
        {
            NativeLibraryResolver.UseRealLibrary();
        }
    }
}
