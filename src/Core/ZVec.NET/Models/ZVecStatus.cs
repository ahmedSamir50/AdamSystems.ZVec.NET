namespace ZVec.NET;

/// <summary>Operation result mirroring native status / error code.</summary>
public sealed class ZVecStatus
{
    /// <summary>The error code returned by the operation.</summary>
    public ZVecErrorCode Code { get; init; }

    /// <summary>Optional error message details.</summary>
    public string? Message { get; init; }

    /// <summary>Indicates whether the operation succeeded (Code == ZVecErrorCode.Ok).</summary>
    public bool IsSuccess => Code == ZVecErrorCode.Ok;
}
