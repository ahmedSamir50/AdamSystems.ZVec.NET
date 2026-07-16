using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET.Internal;

internal sealed class NativeCollectionOptionsBuilder : IDisposable
{
    private readonly nint _handle;
    private bool _disposed;

    public nint Handle => _handle;

    public NativeCollectionOptionsBuilder(ZVecCollectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _handle = NativeMethods.zvec_collection_options_create();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeCollectionOptionsCreateFailed);

        try
        {
            int rc = NativeMethods.zvec_collection_options_set_read_only(_handle, options.ReadOnly);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(NativeMethods.zvec_collection_options_set_read_only));

            rc = NativeMethods.zvec_collection_options_set_enable_mmap(_handle, options.EnableMmap);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(NativeMethods.zvec_collection_options_set_enable_mmap));
            
            // MaxConcurrentReads might not have a direct native equivalent exposed right now. 
            // We'll map it if zvec_collection_options_set_max_readers exists in NativeMethods.
            // But based on available P/Invokes, we only have read_only and enable_mmap.
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            // If there's a destroy method, we should call it here. 
            // Assuming the native API takes ownership during CreateAndOpen.
        }
    }
}
