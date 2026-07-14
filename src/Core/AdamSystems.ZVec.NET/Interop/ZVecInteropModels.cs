using System.Runtime.InteropServices;

namespace AdamSystems.ZVec.NET.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct ZVecString
{
    public IntPtr Str;
    public nuint Len;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ZVecFloatArray
{
    public IntPtr Data;
    public nuint Len;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ZVecByteArray
{
    public IntPtr Data;
    public nuint Len;
}

[StructLayout(LayoutKind.Explicit)]
internal struct ZVecFieldValue
{
    [FieldOffset(0)] public bool BoolValue;
    [FieldOffset(0)] public int Int32Value;
    [FieldOffset(0)] public long Int64Value;
    [FieldOffset(0)] public uint Uint32Value;
    [FieldOffset(0)] public ulong Uint64Value;
    [FieldOffset(0)] public float FloatValue;
    [FieldOffset(0)] public double DoubleValue;
    [FieldOffset(0)] public ZVecString StringValue;
    [FieldOffset(0)] public ZVecFloatArray VectorValue;
    [FieldOffset(0)] public ZVecByteArray BinaryValue;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ZVecWriteResultNative
{
    public int Code;
    public IntPtr Message;
}

