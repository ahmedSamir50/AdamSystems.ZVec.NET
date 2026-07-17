namespace ZVec.NET.Mapping;

/// <summary>
/// Optional collection storage name when it should differ from the CLR type name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ZVecCollectionAttribute : Attribute
{
    /// <summary>Creates a collection name override.</summary>
    /// <param name="name">Native collection name.</param>
    public ZVecCollectionAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Native collection name used in <see cref="ZVecCollectionSchema.Name"/>.</summary>
    public string Name { get; }
}
