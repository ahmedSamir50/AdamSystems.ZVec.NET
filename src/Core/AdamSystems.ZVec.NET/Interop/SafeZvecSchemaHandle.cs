namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// SafeHandle wrapper for native zvec_collection_schema_t pointers.
/// </summary>
internal sealed class SafeZvecSchemaHandle : SafeZvecHandleBase
{
    public SafeZvecSchemaHandle() : base()
    {
    }

    public SafeZvecSchemaHandle(IntPtr handle, bool ownsHandle = true) : base(handle, ownsHandle)
    {
    }

    protected override bool ReleaseHandle()
    {
        // If the factory is not initialized (or already shut down), the native library
        // resources are no longer valid, so calling destroy would cause an Access Violation.
        if (!ZVecFactory.IsNativeLibraryInitialized)
        {
            return true;
        }

        // US-E5.2: Destroy collection schema on release
        if (!IsInvalid)
        {
            NativeMethods.zvec_collection_schema_destroy(handle);
        }
        return true;
    }
}
