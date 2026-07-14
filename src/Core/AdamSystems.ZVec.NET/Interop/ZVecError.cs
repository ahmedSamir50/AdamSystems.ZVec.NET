using System;
using System.Runtime.InteropServices;
using AdamSystems.ZVec.NET.Exceptions;

namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// Helper to map native errors into managed ZVecNativeException exceptions.
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
            throw new ZVecNativeException(code, GetNativeErrorMessage(), context);
        }
    }

    private static string GetNativeErrorMessage()
    {
        try
        {
            // zvec_get_last_error returns a caller-owned allocated char* in the out parameter.
            int rc = NativeMethods.zvec_get_last_error(out IntPtr msgPtr);
            if (msgPtr != IntPtr.Zero)
            {
                var msg = Marshal.PtrToStringUTF8(msgPtr) ?? UnknownErrorMessageFallback;
                // Explicitly free the native memory using zvec_free as mapped in NativeMethods.
                NativeMethods.zvec_free(msgPtr);
                return msg;
            }
        }
        catch (DllNotFoundException)
        {
            // Defensive fallback when native library is not present (e.g. testing before E17)
            return LibraryNotLoadedFallback;
        }

        return UnknownErrorMessageFallback;
    }
}
