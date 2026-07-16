using System.Runtime.InteropServices;
using AdamSystems.ZVec.NET.Exceptions;

namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// Helper to map native errors into managed ZVecNativeException exceptions.
/// Uses zvec_get_last_error_details for structured error information when available.
/// </summary>
internal static class ZVecError
{
    internal const string UnknownErrorMessageFallback = "Unknown error message";
    internal const string LibraryNotLoadedFallback = "Native library not loaded (error details unavailable)";

    /// <summary>
    /// Throws a ZVecNativeException if the given native error code is not Ok.
    /// </summary>
    internal static void ThrowIfFailed(ZVecErrorCode code, string context)
    {
        if (code != ZVecErrorCode.Ok)
        {
            GetNativeErrorDetails(out var message, out var sourceFile, out var sourceLine, out var sourceFunction);
            throw new ZVecNativeException(code, message, context, sourceFile, sourceLine, sourceFunction);
        }
    }

    private static void GetNativeErrorDetails(
        out string message, out string? sourceFile, out int sourceLine, out string? sourceFunction)
    {
        message = UnknownErrorMessageFallback;
        sourceFile = null;
        sourceLine = 0;
        sourceFunction = null;

        try
        {
            // Try structured details first via zvec_get_last_error_details.
            if (TryGetLastErrorDetails(out var detailsMsg, out sourceFile, out sourceLine, out sourceFunction))
            {
                message = detailsMsg;
                return;
            }

            // Fallback to simple message via zvec_get_last_error.
            int rc = NativeMethods.zvec_get_last_error(out IntPtr msgPtr);
            if (msgPtr != IntPtr.Zero)
            {
                message = Marshal.PtrToStringUTF8(msgPtr) ?? UnknownErrorMessageFallback;
                NativeMethods.zvec_free(msgPtr);
            }
        }
        catch (DllNotFoundException)
        {
            message = LibraryNotLoadedFallback;
        }
    }

    private static bool TryGetLastErrorDetails(
        out string message, out string? sourceFile, out int sourceLine, out string? sourceFunction)
    {
        message = UnknownErrorMessageFallback;
        sourceFile = null;
        sourceLine = 0;
        sourceFunction = null;

        // Allocate unmanaged memory for zvec_error_details_t.
        // Layout: zvec_error_code_t code (int) + const char* message + const char* file + int line + const char* function
        int structSize = sizeof(int) + IntPtr.Size + IntPtr.Size + sizeof(int) + IntPtr.Size;
        // Align to pointer size for safety.
        structSize = (structSize + IntPtr.Size - 1) & ~(IntPtr.Size - 1);
        IntPtr detailsPtr = Marshal.AllocHGlobal(structSize);
        try
        {
            // Zero-initialize the struct.
            unsafe
            {
                new Span<byte>((void*)detailsPtr, structSize).Clear();
            }

            int rc = NativeMethods.zvec_get_last_error_details(detailsPtr);
            if (rc != 0) return false;

            // Read fields: code (int at offset 0), message (IntPtr), file (IntPtr), line (int), function (IntPtr)
            int offset = 0;
            // code (int) — skip, we already have it
            offset += sizeof(int);
            // padding to IntPtr alignment
            offset = (offset + IntPtr.Size - 1) & ~(IntPtr.Size - 1);

            IntPtr msgPtr = Marshal.ReadIntPtr(detailsPtr, offset);
            offset += IntPtr.Size;
            IntPtr filePtr = Marshal.ReadIntPtr(detailsPtr, offset);
            offset += IntPtr.Size;
            sourceLine = Marshal.ReadInt32(detailsPtr, offset);
            offset += sizeof(int);
            // padding
            offset = (offset + IntPtr.Size - 1) & ~(IntPtr.Size - 1);
            IntPtr funcPtr = Marshal.ReadIntPtr(detailsPtr, offset);

            message = (msgPtr != IntPtr.Zero) ? Marshal.PtrToStringUTF8(msgPtr) ?? UnknownErrorMessageFallback : UnknownErrorMessageFallback;
            sourceFile = (filePtr != IntPtr.Zero) ? Marshal.PtrToStringUTF8(filePtr) : null;
            sourceFunction = (funcPtr != IntPtr.Zero) ? Marshal.PtrToStringUTF8(funcPtr) : null;

            // Free native strings.
            if (msgPtr != IntPtr.Zero) NativeMethods.zvec_free(msgPtr);
            if (filePtr != IntPtr.Zero) NativeMethods.zvec_free(filePtr);
            if (funcPtr != IntPtr.Zero) NativeMethods.zvec_free(funcPtr);

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(detailsPtr);
        }
    }
}
