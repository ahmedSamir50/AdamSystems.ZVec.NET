using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using AdamSystems.ZVec.NET.Interop;

namespace AdamSystems.ZVec.NET.Internal;

/// <summary>
/// Builds a native zvec_doc_t* from a managed ZVecDoc.
/// Tracks pinned memory and unmanaged allocations for cleanup on Dispose.
/// </summary>
internal sealed class NativeDocBuilder : IDisposable
{
    private IntPtr _handle;
    private readonly List<MemoryHandle> _pinnedHandles = new();
    private readonly List<IntPtr> _unmanagedAllocations = new();
    private bool _disposed;

    public IntPtr Handle => _handle;

    private NativeDocBuilder(IntPtr handle)
    {
        _handle = handle;
    }

    public static NativeDocBuilder Build(ZVecDoc doc)
    {
        IntPtr rawDoc = NativeMethods.zvec_doc_create();
        if (rawDoc == IntPtr.Zero)
        {
            throw new InvalidOperationException(ZVecDefaults.Errors.NativeDocCreateFailed);
        }

        var builder = new NativeDocBuilder(rawDoc);
        try
        {
            NativeMethods.zvec_doc_set_pk(rawDoc, doc.Id);
            NativeMethods.zvec_doc_set_score(rawDoc, doc.Score);

            // Add dense vectors
            foreach (var kvp in doc.DenseVectors)
            {
                builder.AddDenseVectorField(kvp.Key, kvp.Value);
            }

            // Add scalar fields
            foreach (var kvp in doc.Fields)
            {
                builder.AddScalarField(kvp.Key, kvp.Value);
            }
            
            // Note: Sparse vectors are not implemented yet in the native builder per current headers,
            // or if they are, they require a specific struct. We will skip sparse for now or add if needed.
            
            return builder;
        }
        catch
        {
            builder.Dispose();
            throw;
        }
    }

    private unsafe void AddDenseVectorField(string name, ReadOnlyMemory<float> vector)
    {
        var memHandle = vector.Pin();
        _pinnedHandles.Add(memHandle);

        var floatArray = new ZVecFloatArray
        {
            Data = (IntPtr)memHandle.Pointer,
            Len = (nuint)vector.Length
        };

        var fieldValue = new ZVecFieldValue { VectorValue = floatArray };
        int rc = NativeMethods.zvec_doc_add_field_by_value(
            _handle,
            name,
            (int)ZVecDataType.VectorFp32,
            new IntPtr(&fieldValue));

        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AddDenseVectorField));
    }

    private unsafe void AddScalarField(string name, object value)
    {
        ZVecFieldValue fieldValue = default;
        ZVecDataType dataType;

        switch (value)
        {
            case bool b:
                fieldValue.BoolValue = b;
                dataType = ZVecDataType.Bool;
                break;
            case int i:
                fieldValue.Int32Value = i;
                dataType = ZVecDataType.Int32;
                break;
            case long l:
                fieldValue.Int64Value = l;
                dataType = ZVecDataType.Int64;
                break;
            case float f:
                fieldValue.FloatValue = f;
                dataType = ZVecDataType.Float;
                break;
            case double d:
                fieldValue.DoubleValue = d;
                dataType = ZVecDataType.Double;
                break;
            case string s:
                var utf8Bytes = Encoding.UTF8.GetBytes(s);
                IntPtr ptr = Marshal.AllocHGlobal(utf8Bytes.Length + 1); // +1 for null terminator just in case, though len is provided
                _unmanagedAllocations.Add(ptr);
                Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);
                Marshal.WriteByte(ptr, utf8Bytes.Length, 0);
                
                fieldValue.StringValue = new ZVecString
                {
                    Str = ptr,
                    Len = (nuint)utf8Bytes.Length
                };
                dataType = ZVecDataType.String;
                break;
            default:
                throw new NotSupportedException(string.Format(ZVecDefaults.Errors.NativeDataTypeNotSupported, value.GetType()));
        }

        int rc = NativeMethods.zvec_doc_add_field_by_value(
            _handle,
            name,
            (int)dataType,
            new IntPtr(&fieldValue));

        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AddScalarField));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_handle != IntPtr.Zero)
        {
            NativeMethods.zvec_doc_destroy(_handle);
            _handle = IntPtr.Zero;
        }

        foreach (var memHandle in _pinnedHandles)
        {
            memHandle.Dispose();
        }
        _pinnedHandles.Clear();

        foreach (var ptr in _unmanagedAllocations)
        {
            Marshal.FreeHGlobal(ptr);
        }
        _unmanagedAllocations.Clear();
    }
}
