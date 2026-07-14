namespace AdamSystems.ZVec.NET;

/// <summary>Operation result mirroring native status / error code.</summary>
public sealed class ZVecStatus
{
    public ZVecErrorCode Code { get; init; }
    public string? Message { get; init; }
    public bool IsSuccess => Code == ZVecErrorCode.Ok;
}
