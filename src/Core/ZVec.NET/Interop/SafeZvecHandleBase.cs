using Microsoft.Win32.SafeHandles;

namespace ZVec.NET.Interop;

/// <summary>
/// Base class for all ZVec safe handles, providing finalizer diagnostics.
/// </summary>
internal abstract class SafeZvecHandleBase : SafeHandleZeroOrMinusOneIsInvalid
{
    protected SafeZvecHandleBase() : base(ownsHandle: true)
    {
    }

    protected SafeZvecHandleBase(IntPtr handle, bool ownsHandle = true) : base(ownsHandle)
    {
        SetHandle(handle);
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing && !IsInvalid)
        {
            // US-E5.4: Diagnostic warning when called from finalizer thread (missing Dispose)
            Console.Error.WriteLine($"[Warning] SafeHandle of type '{GetType().Name}' (handle: 0x{handle.ToString("X")}) was finalized without being explicitly disposed. Ensure using blocks are used to prevent resource leaks.");
        }
        base.Dispose(disposing);
    }
}
