namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// SafeHandle wrapper for native zvec_collection_t pointers.
/// </summary>
internal sealed class SafeZvecHandle : SafeZvecHandleBase
{
    public SafeZvecHandle() : base()
    {
    }

    public SafeZvecHandle(IntPtr handle, bool ownsHandle = true) : base(handle, ownsHandle)
    {
    }

    protected override bool ReleaseHandle()
    {
        // US-E5.1: SafeHandle finalizer / Dispose safety net: CLOSE ONLY.
        // Never call zvec_collection_destroy here — that permanently deletes on-disk data.
        if (!IsInvalid)
        {
            _ = NativeMethods.zvec_collection_close(handle);
        }
        return true;
    }
}
