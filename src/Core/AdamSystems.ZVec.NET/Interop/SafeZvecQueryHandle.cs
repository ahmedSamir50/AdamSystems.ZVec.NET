namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// SafeHandle wrapper for native zvec_multi_query_t pointers.
/// </summary>
internal sealed class SafeZvecQueryHandle : SafeZvecHandleBase
{
    public SafeZvecQueryHandle() : base()
    {
    }

    public SafeZvecQueryHandle(IntPtr handle, bool ownsHandle = true) : base(handle, ownsHandle)
    {
    }

    protected override bool ReleaseHandle()
    {
        // If the factory is not initialized (or already shut down), the native library
        // resources are no longer valid, so calling destroy would cause an Access Violation.
        if (!ZVecFactory.IsInitialized)
        {
            return true;
        }

        // US-E5.3: Destroy multi-query on release
        if (!IsInvalid)
        {
            NativeMethods.zvec_multi_query_destroy(handle);
        }
        return true;
    }
}
