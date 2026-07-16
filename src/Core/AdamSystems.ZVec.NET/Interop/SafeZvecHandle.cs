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
        // Skip calling native close if the handle is the dummy test pointer (0x12345).
        // This avoids crashing the process with an Access Violation during unit testing.
        if (handle == new IntPtr(0x12345))
        {
            return true;
        }

        // If the factory is not initialized (or already shut down), the native library
        // resources are no longer valid, so calling close would cause an Access Violation.
        if (!ZVecFactory.IsNativeLibraryInitialized)
        {
            return true;
        }

        // US-E5.1: SafeHandle finalizer / Dispose safety net: CLOSE ONLY.
        // Never call zvec_collection_destroy here — that permanently deletes on-disk data.
        if (!IsInvalid)
        {
            _ = NativeMethods.zvec_collection_close(handle);
        }
        return true;
    }
}
