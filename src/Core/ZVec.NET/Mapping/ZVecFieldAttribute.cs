namespace ZVec.NET.Mapping;

/// <summary>
/// Marks a scalar field property. When omitted, public read/write scalar properties are mapped by convention.
/// Use <see cref="Name"/> when the native storage name differs from the CLR property name.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ZVecFieldAttribute : Attribute
{
    /// <summary>Creates a field attribute that uses the property name as the storage name.</summary>
    public ZVecFieldAttribute()
    {
    }

    /// <summary>Creates a field attribute with an explicit native storage name.</summary>
    /// <param name="name">Native field name stored in ZVec.</param>
    public ZVecFieldAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>Optional native storage name. When null, the CLR property name is used.</summary>
    public string? Name { get; }

    /// <summary>Whether the field is nullable in the schema. Default is false.</summary>
    public bool Nullable { get; set; }
}
