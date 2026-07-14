using System;

namespace AdamSystems.ZVec.NET.Exceptions;

/// <summary>
/// Base exception class for all ZVec.NET related issues.
/// </summary>
public class ZVecException : Exception
{
    public ZVecException() { }
    public ZVecException(string message) : base(message) { }
    public ZVecException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when the native library SemVer version mismatches what the C# wrapper expects.
/// </summary>
public class ZVecAbiMismatchException : ZVecException
{
    public string ExpectedVersion { get; }
    public string FoundVersion { get; }

    public ZVecAbiMismatchException(string expected, string found)
        : base($"ZVec ABI Version mismatch. Expected native library version: '{expected}', but found: '{found}'. Please update your native binaries.")
    {
        ExpectedVersion = expected;
        FoundVersion = found;
    }
}

/// <summary>
/// Thrown when a native operation fails with a ZVec native error code.
/// </summary>
public class ZVecNativeException : ZVecException
{
    public ZVecErrorCode ErrorCode { get; }
    public string NativeErrorMessage { get; }
    public string OperationContext { get; }

    public ZVecNativeException(ZVecErrorCode code, string nativeMessage, string context)
        : base($"ZVec native operation failed. Error Code: {code} ({context}). Native Details: {nativeMessage}")
    {
        ErrorCode = code;
        NativeErrorMessage = nativeMessage;
        OperationContext = context;
    }
}
