namespace AdamSystems.ZVec.NET;

/// <summary>
/// Represents the result of a write operation (insert/update/delete) for a single document.
/// </summary>
public readonly struct ZVecWriteResult
{
    /// <summary>
    /// The status code of the operation.
    /// </summary>
    public ZVecErrorCode Code { get; init; }

    /// <summary>
    /// The error message, if any.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    public bool IsSuccess => Code == ZVecErrorCode.Ok;
}
