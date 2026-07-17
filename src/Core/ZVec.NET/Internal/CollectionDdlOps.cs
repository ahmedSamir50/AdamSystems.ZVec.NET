using ZVec.NET.Interop;

namespace ZVec.NET.Internal;

/// <summary>Column / index DDL and optimize.</summary>
internal sealed class CollectionDdlOps
{
    private readonly CollectionNativeContext _ctx;

    public CollectionDdlOps(CollectionNativeContext ctx) => _ctx = ctx;

    public void AddColumn(ZVecFieldSchema field, string? defaultExpression = null)
    {
        _ctx.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(field);

        _ctx.Gate.EnterNativeCall();
        try
        {
            using var builder = new NativeFieldSchemaBuilder(field);
            int rc = NativeMethods.zvec_collection_add_column(_ctx.Handle, builder.Handle, defaultExpression ?? string.Empty);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AddColumn));
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }

        if (_ctx.Schema != null)
        {
            var fields = _ctx.Schema.Fields.ToList();
            fields.Add(field);
            _ctx.Schema = new ZVecCollectionSchema
            {
                Name = _ctx.Schema.Name,
                Fields = fields,
                Vectors = _ctx.Schema.Vectors
            };
        }
    }

    public void DropColumn(string columnName)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        _ctx.Gate.EnterNativeCall();
        try
        {
            int rc = NativeMethods.zvec_collection_drop_column(_ctx.Handle, columnName);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DropColumn));
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }

        if (_ctx.Schema != null)
        {
            var fields = _ctx.Schema.Fields.Where(f => f.Name != columnName).ToList();
            var vectors = _ctx.Schema.Vectors.Where(v => v.Name != columnName).ToList();
            _ctx.Schema = new ZVecCollectionSchema
            {
                Name = _ctx.Schema.Name,
                Fields = fields,
                Vectors = vectors
            };
        }
    }

    public void AlterColumn(string columnName, string? newName = null, ZVecFieldSchema? newSchema = null)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        _ctx.Gate.EnterNativeCall();
        try
        {
            using var builder = newSchema != null ? new NativeFieldSchemaBuilder(newSchema) : null;
            int rc = NativeMethods.zvec_collection_alter_column(
                _ctx.Handle,
                columnName,
                newName,
                builder?.Handle ?? IntPtr.Zero);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AlterColumn));
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }

        if (_ctx.Schema != null)
        {
            var fields = _ctx.Schema.Fields.ToList();
            var target = fields.FirstOrDefault(f => f.Name == columnName);
            if (target != null)
            {
                var replacement = newSchema ?? target;
                var finalField = new ZVecFieldSchema
                {
                    Name = newName ?? replacement.Name,
                    DataType = replacement.DataType,
                    Nullable = replacement.Nullable
                };
                fields[fields.IndexOf(target)] = finalField;
                _ctx.Schema = new ZVecCollectionSchema
                {
                    Name = _ctx.Schema.Name,
                    Fields = fields,
                    Vectors = _ctx.Schema.Vectors
                };
            }
        }
    }

    public void CreateIndex(string columnName, ZVecIndexParam indexParam)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        ArgumentNullException.ThrowIfNull(indexParam);

        _ctx.Gate.EnterNativeCall();
        try
        {
            using var builder = new NativeIndexParamBuilder(indexParam);
            int rc = NativeMethods.zvec_collection_create_index(_ctx.Handle, columnName, builder.Handle);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(CreateIndex));
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    public void DropIndex(string columnName)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        _ctx.Gate.EnterNativeCall();
        try
        {
            int rc = NativeMethods.zvec_collection_drop_index(_ctx.Handle, columnName);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DropIndex));
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    public void Optimize()
    {
        _ctx.ThrowIfDisposed();

        _ctx.Gate.EnterNativeCall();
        try
        {
            int rc = NativeMethods.zvec_collection_optimize(_ctx.Handle);
            ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Optimize));
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }
}
