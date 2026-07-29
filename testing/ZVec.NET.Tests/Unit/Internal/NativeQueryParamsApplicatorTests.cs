using FluentAssertions;
using ZVec.NET.Internal;

namespace ZVec.NET.Tests.Unit.Internal;

public class NativeQueryParamsApplicatorTests
{
    [Fact]
    public void Apply_Null_QueryParams_Is_NoOp()
    {
        var act = () => NativeQueryParamsApplicator.Apply(
            IntPtr.Zero,
            null,
            NativeQueryParamsApplicator.QueryTarget.VectorQuery);
        act.Should().NotThrow();
    }

    [Fact]
    public void Apply_Unsupported_QueryParams_Type_Throws()
    {
        var act = () => NativeQueryParamsApplicator.Apply(
            IntPtr.Zero,
            new UnsupportedQueryParams(),
            NativeQueryParamsApplicator.QueryTarget.VectorQuery);
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*UnsupportedQueryParams*");
    }

    private sealed class UnsupportedQueryParams : ZVecQueryParams;
}
