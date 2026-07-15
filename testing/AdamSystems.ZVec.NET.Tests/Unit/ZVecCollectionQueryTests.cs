using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit;

public class ZVecCollectionQueryTests
{
    private const nint FakeHandle = 1;

    private static ZVecCollection CreateCollection()
        => new(FakeHandle, "/tmp/test-collection", schema: null, CancellationToken.None);

    [Fact]
    public void Query_WithNullQuery_ThrowsArgumentNullException()
    {
        var col = CreateCollection();
        var act = () => col.Query((ZVecQuery)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Query_WhenDisposed_ThrowsObjectDisposedException()
    {
        var col = CreateCollection();
        try { col.Dispose(); } catch { }
        var act = () => col.Query(new ZVecQuery { FieldName = "vec" });
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Query_AttemptsNativeCall()
    {
        var col = CreateCollection();
        var q = new ZVecQuery { FieldName = "vec", Vector = new float[] { 1.0f, 2.0f } };
        var act = () => col.Query(q);
        act.Should().Throw<Exception>().Where(ex => ex is DllNotFoundException || ex is EntryPointNotFoundException);
    }

    [Fact]
    public async Task QueryAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var col = CreateCollection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var q = new ZVecQuery { FieldName = "vec" };
        var act = async () => await col.QueryAsync(q, ct: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
