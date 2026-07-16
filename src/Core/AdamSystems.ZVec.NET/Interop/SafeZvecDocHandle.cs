using System.Runtime.InteropServices;

namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// SafeHandle wrapping a native zvec_doc_t*.
/// </summary>
internal sealed class SafeZvecDocHandle : SafeHandle
{
    public SafeZvecDocHandle() : base(IntPtr.Zero, true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    protected override bool ReleaseHandle()
    {
        // If the factory is not initialized (or already shut down), the native library
        // Wait! ZVecFactory.Shutdown() might have unloaded the library.
        // Calling into an unloaded DLL causes 0xC0000005 Access Violation.
        if (!ZVecFactory.IsNativeLibraryInitialized)
        {
            return true;
        }

        if (handle != IntPtr.Zero)
        {
            NativeMethods.zvec_doc_destroy(handle);
            handle = IntPtr.Zero;
        }
        return true;
    }
}
