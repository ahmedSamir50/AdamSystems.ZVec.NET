namespace AdamSystems.ZVec.NET;

/// <summary>Maps to <c>zvec_error_code_t</c>.</summary>
public enum ZVecErrorCode
{
    Ok = 0,
    NotFound = 1,
    AlreadyExists = 2,
    InvalidArgument = 3,
    PermissionDenied = 4,
    FailedPrecondition = 5,
    ResourceExhausted = 6,
    Unavailable = 7,
    InternalError = 8,
    NotSupported = 9,
    Unknown = 10
}
