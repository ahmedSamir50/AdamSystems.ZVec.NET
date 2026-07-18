using System.Runtime.InteropServices;
using ZVec.NET.Exceptions;

namespace ZVec.NET.Interop;

/// <summary>
/// Helper to map native errors into managed ZVecNativeException exceptions.
/// Uses zvec_get_last_error_details for structured error information when available.
/// </summary>
internal static class ZVecError
{
    internal const string UnknownErrorMessageFallback = "Unknown error message";
    internal const string LibraryNotLoadedFallback = "Native library not loaded (error details unavailable)";

    /// <summary>
    /// Native <c>zvec_error_details_t</c> layout. Must match C ABI (padding on 64-bit → 40 bytes).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeErrorDetails
    {
        public int Code;
        public IntPtr Message;
        public IntPtr File;
        public int Line;
        public IntPtr Function;
    }

    /// <summary>Unmanaged size of <see cref="NativeErrorDetails"/> (40 on 64-bit).</summary>
    internal static int NativeErrorDetailsSize => Marshal.SizeOf<NativeErrorDetails>();

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

            // Fallback to simple message via zvec_get_last_error (caller-owned; must free).
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

        // Allocate full C ABI size (includes pointer alignment padding). Manual 32-byte
        // field-sum under-allocated on x64 and caused STATUS_HEAP_CORRUPTION (0xC0000374).
        int structSize = NativeErrorDetailsSize;
        IntPtr detailsPtr = Marshal.AllocHGlobal(structSize);
        try
        {
            unsafe
            {
                new Span<byte>((void*)detailsPtr, structSize).Clear();
            }

            int rc = NativeMethods.zvec_get_last_error_details(detailsPtr);
            if (rc != 0) return false;

            var details = Marshal.PtrToStructure<NativeErrorDetails>(detailsPtr);

            // Pointers from zvec_get_last_error_details refer to TLS / string literals —
            // do NOT zvec_free them (that also corrupted the heap).
            message = details.Message != IntPtr.Zero
                ? Marshal.PtrToStringUTF8(details.Message) ?? UnknownErrorMessageFallback
                : UnknownErrorMessageFallback;
            sourceFile = details.File != IntPtr.Zero ? Marshal.PtrToStringUTF8(details.File) : null;
            sourceLine = details.Line;
            sourceFunction = details.Function != IntPtr.Zero
                ? Marshal.PtrToStringUTF8(details.Function)
                : null;

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
