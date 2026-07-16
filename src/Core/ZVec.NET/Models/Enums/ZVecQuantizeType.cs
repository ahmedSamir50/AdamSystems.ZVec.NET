namespace ZVec.NET;

/// <summary>Maps to <c>zvec::QuantizeType</c> in <c>type.h</c>.</summary>
public enum ZVecQuantizeType
{
    Undefined = 0,
    Fp16 = 1,
    Int8 = 2,
    Int4 = 3,
    /// <summary>Defined in <c>type.h</c>; missing from <c>c_api.h</c> macros â€” value is 4.</summary>
    Rabitq = 4
}
