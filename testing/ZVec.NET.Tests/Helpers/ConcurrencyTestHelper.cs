namespace ZVec.NET.Tests.Helpers;

/// <summary>
/// Barrier-synchronized concurrency helpers for memory and stress tests.
/// </summary>
public static class ConcurrencyTestHelper
{
    /// <summary>
    /// Launches <paramref name="threadCount"/> workers that start together via a barrier.
    /// </summary>
    public static async Task RunConcurrently(
        int threadCount,
        Func<int, CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threadCount);
        ArgumentNullException.ThrowIfNull(action);

        using var barrier = new Barrier(threadCount);
        var tasks = new Task[threadCount];

        for (int i = 0; i < threadCount; i++)
        {
            int workerId = i;
            tasks[i] = Task.Run(async () =>
            {
                barrier.SignalAndWait(cancellationToken);
                await action(workerId, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs <paramref name="action"/> and fails if it does not complete within <paramref name="timeout"/>.
    /// </summary>
    public static async Task VerifyNoDeadlock(
        Func<CancellationToken, Task> action,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await action(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Action did not complete within {timeout.TotalSeconds:0.###}s (possible deadlock).");
        }
    }
}
