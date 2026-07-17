namespace ZVec.NET.Mapping;

/// <summary>
/// Excludes a public property from typed schema mapping and document marshalling.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ZVecIgnoreAttribute : Attribute
{
}
