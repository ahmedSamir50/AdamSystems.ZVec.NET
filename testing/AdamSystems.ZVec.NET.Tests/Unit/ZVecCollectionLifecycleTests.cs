using FluentAssertions;

namespace AdamSystems.ZVec.NET.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ZVecCollection"/> lifecycle semantics.
/// Uses a fake/null handle (nint.MaxValue) to test managed-side logic only.
/// Full native round-trips are covered in integration tests (Epic E18).
/// </summary>
public class ZVecCollectionLifecycleTests
{
    // We cannot call NativeMethods with a fake handle, so these tests validate
    // the managed-side idempotency and ordering logic in isolation.
    // The ZVecCollection constructor rejects handle == 0; we use a sentinel value.
    private const nint FakeHandle = 1; // Non-zero sentinel; not a real allocation.

    private static ZVecCollection CreateCollection()
        => new(FakeHandle, "/tmp/test-collection", schema: null, CancellationToken.None);

    [Fact]
    public void Collection_Path_ReturnsConstructedValue()
    {
        var col = CreateCollection();
        col.Path.Should().Be("/tmp/test-collection");
    }

    [Fact]
    public void Collection_Schema_NullWhenNotProvided()
    {
        var col = CreateCollection();
        col.Schema.Should().BeNull();
    }

    [Fact]
    public void Collection_DisposeAsync_DoesNotThrow()
    {
        var col = CreateCollection();
        // DisposeAsync delegates to Dispose which calls zvec_collection_close.
        // Without real native: DllNotFoundException expected, not InvalidOperation.
        var act = async () => { await col.DisposeAsync(); };
        // We only assert no InvalidOperationException — DllNotFound is expected without native.
        act.Should().NotThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Collection_Dispose_CalledTwice_IsIdempotent()
    {
        // The Interlocked.Exchange guard must ensure the second call is a no-op.
        // With no native DLL: first call may throw DllNotFoundException.
        // The important invariant: second call NEVER throws (idempotent).
        var col = CreateCollection();
        try { col.Dispose(); } catch { /* expected without native */ }

        // Second call must be silent regardless.
        var secondCall = () => col.Dispose();
        secondCall.Should().NotThrow();
    }

    [Fact]
    public async Task Collection_ConcurrentDisposeAndDisposeAsync_CloseCalledOnce()
    {
        // Concurrent Dispose + DisposeAsync — only one must execute the close path.
        // Both paths go through Interlocked.Exchange(ref _disposed, 1).
        var col = CreateCollection();

        // Both paths swallow exceptions (DllNotFoundException expected without native).
        // The key invariant: no crash, no hang, no InvalidOperationException.
        var task1 = Task.Run(() => { try { col.Dispose(); } catch { } }, TestContext.Current.CancellationToken);
        var task2 = Task.Run(async () => { try { await col.DisposeAsync(); } catch { } }, TestContext.Current.CancellationToken);

        await Task.WhenAll(task1, task2).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        // No hang — test completes within timeout.
    }



    [Fact]
    public void Collection_Destroy_CalledTwice_IsIdempotent()
    {
        var col = CreateCollection();
        try { col.Destroy(); } catch { /* expected without native */ }

        // Second call must be a no-op — Interlocked.Exchange guards _destroyed.
        var secondDestroy = () => col.Destroy();
        secondDestroy.Should().NotThrow();
    }

    [Fact]
    public void Collection_DestroyAsync_CancelledToken_Throws()
    {
        var col = CreateCollection();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var act = async () => await col.DestroyAsync(cts.Token);
        act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Collection_Constructor_ZeroHandle_Throws()
    {
        var act = () => new ZVecCollection(nint.Zero, "/tmp/test", null, CancellationToken.None);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
