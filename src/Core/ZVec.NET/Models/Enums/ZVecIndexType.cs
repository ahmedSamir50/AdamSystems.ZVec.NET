namespace ZVec.NET;

/// <summary>Maps to <c>zvec::IndexType</c> in <c>type.h</c>.</summary>
public enum ZVecIndexType
{
    Undefined = 0,
    Hnsw = 1,
    Ivf = 2,
    Flat = 3,
    /// <summary>Defined in <c>type.h</c>; missing from <c>c_api.h</c> macros — value is 4.</summary>
    HnswRabitq = 4,
    DiskAnn = 5,
    Vamana = 6,
    Invert = 10,
    Fts = 11
}
