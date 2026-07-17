using System.Globalization;

namespace ZVec.NET.Exceptions;

/// <summary>
/// Thrown when a typed document model does not match the open collection schema
/// (for example a mapped property has no corresponding native column).
/// </summary>
public sealed class ZVecSchemaMismatchException : ZVecException
{
    /// <summary>Creates a schema mismatch exception.</summary>
    /// <param name="clrTypeName">Mapped CLR type name.</param>
    /// <param name="detail">Human-readable mismatch detail.</param>
    public ZVecSchemaMismatchException(string clrTypeName, string detail)
        : base(string.Format(
            CultureInfo.InvariantCulture,
            ZVecDefaults.Errors.MappingSchemaMismatch,
            clrTypeName,
            detail))
    {
        ClrTypeName = clrTypeName;
        Detail = detail;
    }

    /// <summary>Mapped CLR type name.</summary>
    public string ClrTypeName { get; }

    /// <summary>Mismatch detail.</summary>
    public string Detail { get; }
}
