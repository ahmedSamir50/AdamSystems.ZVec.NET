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
/// Carries structured error details from the native layer when available.
/// </summary>
public class ZVecNativeException : ZVecException
{
    public ZVecErrorCode ErrorCode { get; }
    public string NativeErrorMessage { get; }
    public string OperationContext { get; }
    public string? SourceFile { get; }
    public int SourceLine { get; }
    public string? SourceFunction { get; }

    public ZVecNativeException(ZVecErrorCode code, string nativeMessage, string context,
        string? sourceFile = null, int sourceLine = 0, string? sourceFunction = null)
        : base(FormatMessage(code, nativeMessage, context, sourceFile, sourceLine, sourceFunction))
    {
        ErrorCode = code;
        NativeErrorMessage = nativeMessage;
        OperationContext = context;
        SourceFile = sourceFile;
        SourceLine = sourceLine;
        SourceFunction = sourceFunction;
    }

    private static string FormatMessage(ZVecErrorCode code, string nativeMessage, string context,
        string? sourceFile, int sourceLine, string? sourceFunction)
    {
        var sb = $"ZVec native operation failed. Error Code: {code} ({context}). Native Details: {nativeMessage}";
        if (!string.IsNullOrEmpty(sourceFunction) || !string.IsNullOrEmpty(sourceFile))
        {
            sb += $" at {sourceFunction ?? "?"} in {sourceFile ?? "?"}:{sourceLine}";
        }
        return sb;
    }
}
