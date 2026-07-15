using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit;

public class ZVecCollectionCrudTests
{
    private const nint FakeHandle = 1;

    private static ZVecCollection CreateCollection()
        => new(FakeHandle, "/tmp/test-collection", schema: null, CancellationToken.None);

    [Fact]
    public void Insert_WithNullDoc_ThrowsArgumentNullException()
    {
        var col = CreateCollection();
        var act = () => col.Insert((ZVecDoc)null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Insert_WhenDisposed_ThrowsObjectDisposedException()
    {
        var col = CreateCollection();
        try { col.Dispose(); } catch { }
        var act = () => col.Insert(ZVecDoc.Create("id1"));
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Insert_AttemptsNativeCall()
    {
        var col = CreateCollection();
        var act = () => col.Insert(ZVecDoc.Create("id1"));
        act.Should().Throw<Exception>().Where(ex => ex is DllNotFoundException || ex is EntryPointNotFoundException);
    }

    [Fact]
    public async Task InsertAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        var col = CreateCollection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = async () => await col.InsertAsync(ZVecDoc.Create("id1"), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Fetch_WithNullKey_ThrowsArgumentNullException()
    {
        var col = CreateCollection();
        var act = () => col.Fetch((string)null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Fetch_AttemptsNativeCall()
    {
        var col = CreateCollection();
        var act = () => col.Fetch(["id1"]);
        act.Should().Throw<Exception>().Where(ex => ex is DllNotFoundException || ex is EntryPointNotFoundException);
    }
}
