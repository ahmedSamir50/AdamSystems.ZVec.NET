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
            
            // Add sparse vectors
            foreach (var kvp in doc.SparseVectors)
            {
                builder.AddSparseVectorField(kvp.Key, kvp.Value);
            }
            
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

        // Pass the raw float* pointer and the byte size of the vector directly.
        // The real C API signature is: zvec_doc_add_field_by_value(doc, name, type, value, value_size)
        // where value_size = count * sizeof(float).
        nuint valueSize = (nuint)(vector.Length * sizeof(float));

        int rc = NativeMethods.zvec_doc_add_field_by_value(
            _handle,
            name,
            (int)ZVecDataType.VectorFp32,
            (IntPtr)memHandle.Pointer,
            valueSize);

        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AddDenseVectorField));
    }

    private unsafe void AddSparseVectorField(string name, IReadOnlyDictionary<int, float> vector)
    {
        var count = vector.Count;
        var indicesPtr = Marshal.AllocHGlobal(count * sizeof(int));
        var valuesPtr = Marshal.AllocHGlobal(count * sizeof(float));
        _unmanagedAllocations.Add(indicesPtr);
        _unmanagedAllocations.Add(valuesPtr);

        var indices = new Span<int>((void*)indicesPtr, count);
        var values = new Span<float>((void*)valuesPtr, count);

        int i = 0;
        foreach (var kvp in vector)
        {
            indices[i] = kvp.Key;
            values[i] = kvp.Value;
            i++;
        }

        int rc = NativeMethods.zvec_doc_add_sparse_vector_field(
            _handle,
            name,
            indicesPtr,
            valuesPtr,
            (nuint)count);

        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AddSparseVectorField));
    }

    private unsafe void AddScalarField(string name, object value)
    {
        // The real C API signature is:
        //   zvec_doc_add_field_by_value(doc, name, data_type, const void* value, size_t value_size)
        // where value is a raw pointer to the typed payload and value_size is its byte size.
        IntPtr valuePtr;
        nuint valueSize;
        ZVecDataType dataType;

        switch (value)
        {
            case bool b:
            {
                byte bv = b ? (byte)1 : (byte)0;
                var ptr = Marshal.AllocHGlobal(1);
                _unmanagedAllocations.Add(ptr);
                Marshal.WriteByte(ptr, bv);
                valuePtr = ptr; valueSize = 1; dataType = ZVecDataType.Bool;
                break;
            }
            case int i:
            {
                var ptr = Marshal.AllocHGlobal(sizeof(int));
                _unmanagedAllocations.Add(ptr);
                Marshal.WriteInt32(ptr, i);
                valuePtr = ptr; valueSize = sizeof(int); dataType = ZVecDataType.Int32;
                break;
            }
            case long l:
            {
                var ptr = Marshal.AllocHGlobal(sizeof(long));
                _unmanagedAllocations.Add(ptr);
                Marshal.WriteInt64(ptr, l);
                valuePtr = ptr; valueSize = sizeof(long); dataType = ZVecDataType.Int64;
                break;
            }
            case uint ui:
            {
                var ptr = Marshal.AllocHGlobal(sizeof(uint));
                _unmanagedAllocations.Add(ptr);
                Marshal.WriteInt32(ptr, (int)ui);
                valuePtr = ptr; valueSize = sizeof(uint); dataType = ZVecDataType.UInt32;
                break;
            }
            case ulong ul:
            {
                var ptr = Marshal.AllocHGlobal(sizeof(ulong));
                _unmanagedAllocations.Add(ptr);
                Marshal.WriteInt64(ptr, (long)ul);
                valuePtr = ptr; valueSize = sizeof(ulong); dataType = ZVecDataType.UInt64;
                break;
            }
            case float f:
            {
                var ptr = Marshal.AllocHGlobal(sizeof(float));
                _unmanagedAllocations.Add(ptr);
                *(float*)ptr = f;
                valuePtr = ptr; valueSize = sizeof(float); dataType = ZVecDataType.Float;
                break;
            }
            case double d:
            {
                var ptr = Marshal.AllocHGlobal(sizeof(double));
                _unmanagedAllocations.Add(ptr);
                *(double*)ptr = d;
                valuePtr = ptr; valueSize = sizeof(double); dataType = ZVecDataType.Double;
                break;
            }
            case string s:
            {
                var utf8Bytes = Encoding.UTF8.GetBytes(s);
                IntPtr ptr = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
                _unmanagedAllocations.Add(ptr);
                Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);
                Marshal.WriteByte(ptr, utf8Bytes.Length, 0);
                valuePtr = ptr; valueSize = (nuint)utf8Bytes.Length; dataType = ZVecDataType.String;
                break;
            }
            default:
                throw new NotSupportedException(string.Format(ZVecDefaults.Errors.NativeDataTypeNotSupported, value.GetType()));
        }

        int rc = NativeMethods.zvec_doc_add_field_by_value(
            _handle,
            name,
            (int)dataType,
            valuePtr,
            valueSize);

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
