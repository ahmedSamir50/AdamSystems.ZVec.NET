using System.Collections.Concurrent;

namespace ZVec.NET.Mapping;

/// <summary>Maps CLR property types to <see cref="ZVecDataType"/> for scalar fields.</summary>
internal static class ZVecClrTypeMap
{
    private static readonly ConcurrentDictionary<Type, ZVecDataType?> Cache = new();

    public static bool TryGetScalarDataType(Type clrType, out ZVecDataType dataType)
    {
        ArgumentNullException.ThrowIfNull(clrType);
        var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;
        var mapped = Cache.GetOrAdd(underlying, static t => MapScalar(t));
        if (mapped is null)
        {
            dataType = ZVecDataType.Undefined;
            return false;
        }

        dataType = mapped.Value;
        return true;
    }

    public static bool IsDenseVectorMemory(Type clrType)
        => clrType == typeof(ReadOnlyMemory<float>) || clrType == typeof(float[]);

    public static bool IsSparseVectorDictionary(Type clrType)
        => clrType == typeof(IReadOnlyDictionary<int, float>)
           || clrType == typeof(Dictionary<int, float>)
           || clrType == typeof(IDictionary<int, float>);

    private static ZVecDataType? MapScalar(Type t) => t switch
    {
        _ when t == typeof(string) => ZVecDataType.String,
        _ when t == typeof(bool) => ZVecDataType.Bool,
        _ when t == typeof(int) => ZVecDataType.Int32,
        _ when t == typeof(long) => ZVecDataType.Int64,
        _ when t == typeof(uint) => ZVecDataType.UInt32,
        _ when t == typeof(ulong) => ZVecDataType.UInt64,
        _ when t == typeof(float) => ZVecDataType.Float,
        _ when t == typeof(double) => ZVecDataType.Double,
        _ when t == typeof(byte[]) => ZVecDataType.Binary,
        _ when t == typeof(string[]) => ZVecDataType.ArrayString,
        _ when t == typeof(bool[]) => ZVecDataType.ArrayBool,
        _ when t == typeof(int[]) => ZVecDataType.ArrayInt32,
        _ when t == typeof(long[]) => ZVecDataType.ArrayInt64,
        _ when t == typeof(uint[]) => ZVecDataType.ArrayUInt32,
        _ when t == typeof(ulong[]) => ZVecDataType.ArrayUInt64,
        _ when t == typeof(float[]) => null, // dense vector — handled separately
        _ when t == typeof(double[]) => ZVecDataType.ArrayDouble,
        _ => null
    };
}
