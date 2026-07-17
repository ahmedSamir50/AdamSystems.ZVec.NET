namespace ZVec.NET.Mapping;

/// <summary>Role of a mapped CLR property in a ZVec document type.</summary>
public enum ZVecPropertyKind
{
    /// <summary>Document primary key.</summary>
    Id = 0,

    /// <summary>Scalar field.</summary>
    Field = 1,

    /// <summary>Dense or sparse vector field.</summary>
    Vector = 2
}
