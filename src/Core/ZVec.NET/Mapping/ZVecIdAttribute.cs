namespace ZVec.NET.Mapping;

/// <summary>
/// Marks the document identity property. Exactly one identity is required per mapped type
/// (this attribute, or the <c>Id</c>/<c>ID</c> name convention).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ZVecIdAttribute : Attribute
{
}
