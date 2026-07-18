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
            AddColumnCore(field, defaultExpression);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }

        RefreshSchemaAfterAdd(field);
    }

    public ValueTask AddColumnAsync(
        ZVecFieldSchema field,
        string? defaultExpression = null,
        CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(field);
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        if (!_ctx.Gate.NeedsAsyncWaitForNative)
        {
            try
            {
                AddColumn(field, defaultExpression);
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }

        return AddColumnAsyncCore(field, defaultExpression, ct);
    }

    private async ValueTask AddColumnAsyncCore(
        ZVecFieldSchema field,
        string? defaultExpression,
        CancellationToken ct)
    {
        await _ctx.Gate.EnterNativeCallAsync(ct).ConfigureAwait(false);
        try
        {
            AddColumnCore(field, defaultExpression);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }

        RefreshSchemaAfterAdd(field);
    }

    private void AddColumnCore(ZVecFieldSchema field, string? defaultExpression)
    {
        using var builder = new NativeFieldSchemaBuilder(field);
        int rc = NativeMethods.zvec_collection_add_column(_ctx.Handle, builder.Handle, defaultExpression ?? string.Empty);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AddColumn));
    }

    private void RefreshSchemaAfterAdd(ZVecFieldSchema field)
    {
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
            DropColumnCore(columnName);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }

        RefreshSchemaAfterDrop(columnName);
    }

    public ValueTask DropColumnAsync(string columnName, CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        if (!_ctx.Gate.NeedsAsyncWaitForNative)
        {
            try
            {
                DropColumn(columnName);
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }

        return DropColumnAsyncCore(columnName, ct);
    }

    private async ValueTask DropColumnAsyncCore(string columnName, CancellationToken ct)
    {
        await _ctx.Gate.EnterNativeCallAsync(ct).ConfigureAwait(false);
        try
        {
            DropColumnCore(columnName);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }

        RefreshSchemaAfterDrop(columnName);
    }

    private void DropColumnCore(string columnName)
    {
        int rc = NativeMethods.zvec_collection_drop_column(_ctx.Handle, columnName);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DropColumn));
    }

    private void RefreshSchemaAfterDrop(string columnName)
    {
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
            AlterColumnCore(columnName, newName, newSchema);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }

        RefreshSchemaAfterAlter(columnName, newName, newSchema);
    }

    public ValueTask AlterColumnAsync(
        string columnName,
        string? newName = null,
        ZVecFieldSchema? newSchema = null,
        CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        if (!_ctx.Gate.NeedsAsyncWaitForNative)
        {
            try
            {
                AlterColumn(columnName, newName, newSchema);
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }

        return AlterColumnAsyncCore(columnName, newName, newSchema, ct);
    }

    private async ValueTask AlterColumnAsyncCore(
        string columnName,
        string? newName,
        ZVecFieldSchema? newSchema,
        CancellationToken ct)
    {
        await _ctx.Gate.EnterNativeCallAsync(ct).ConfigureAwait(false);
        try
        {
            AlterColumnCore(columnName, newName, newSchema);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }

        RefreshSchemaAfterAlter(columnName, newName, newSchema);
    }

    private void AlterColumnCore(string columnName, string? newName, ZVecFieldSchema? newSchema)
    {
        using var builder = newSchema != null ? new NativeFieldSchemaBuilder(newSchema) : null;
        int rc = NativeMethods.zvec_collection_alter_column(
            _ctx.Handle,
            columnName,
            newName,
            builder?.Handle ?? IntPtr.Zero);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(AlterColumn));
    }

    private void RefreshSchemaAfterAlter(string columnName, string? newName, ZVecFieldSchema? newSchema)
    {
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
            CreateIndexCore(columnName, indexParam);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    public ValueTask CreateIndexAsync(string columnName, ZVecIndexParam indexParam, CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        ArgumentNullException.ThrowIfNull(indexParam);
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        if (!_ctx.Gate.NeedsAsyncWaitForNative)
        {
            try
            {
                CreateIndex(columnName, indexParam);
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }

        return CreateIndexAsyncCore(columnName, indexParam, ct);
    }

    private async ValueTask CreateIndexAsyncCore(string columnName, ZVecIndexParam indexParam, CancellationToken ct)
    {
        await _ctx.Gate.EnterNativeCallAsync(ct).ConfigureAwait(false);
        try
        {
            CreateIndexCore(columnName, indexParam);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    private void CreateIndexCore(string columnName, ZVecIndexParam indexParam)
    {
        using var builder = new NativeIndexParamBuilder(indexParam);
        int rc = NativeMethods.zvec_collection_create_index(_ctx.Handle, columnName, builder.Handle);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(CreateIndex));
    }

    public void DropIndex(string columnName)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

        _ctx.Gate.EnterNativeCall();
        try
        {
            DropIndexCore(columnName);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    public ValueTask DropIndexAsync(string columnName, CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(columnName);
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        if (!_ctx.Gate.NeedsAsyncWaitForNative)
        {
            try
            {
                DropIndex(columnName);
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }

        return DropIndexAsyncCore(columnName, ct);
    }

    private async ValueTask DropIndexAsyncCore(string columnName, CancellationToken ct)
    {
        await _ctx.Gate.EnterNativeCallAsync(ct).ConfigureAwait(false);
        try
        {
            DropIndexCore(columnName);
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    private void DropIndexCore(string columnName)
    {
        int rc = NativeMethods.zvec_collection_drop_index(_ctx.Handle, columnName);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(DropIndex));
    }

    public void Optimize()
    {
        _ctx.ThrowIfDisposed();

        _ctx.Gate.EnterNativeCall();
        try
        {
            OptimizeCore();
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    public ValueTask OptimizeAsync(CancellationToken ct = default)
    {
        _ctx.ThrowIfDisposed();
        if (ct.IsCancellationRequested) return ValueTask.FromCanceled(ct);
        if (!_ctx.Gate.NeedsAsyncWaitForNative)
        {
            try
            {
                Optimize();
                return ValueTask.CompletedTask;
            }
            catch (Exception ex)
            {
                return ValueTask.FromException(ex);
            }
        }

        return OptimizeAsyncCore(ct);
    }

    private async ValueTask OptimizeAsyncCore(CancellationToken ct)
    {
        await _ctx.Gate.EnterNativeCallAsync(ct).ConfigureAwait(false);
        try
        {
            OptimizeCore();
        }
        finally
        {
            _ctx.Gate.ExitNativeCall();
        }
    }

    private void OptimizeCore()
    {
        int rc = NativeMethods.zvec_collection_optimize(_ctx.Handle);
        ZVecError.ThrowIfFailed((ZVecErrorCode)rc, nameof(Optimize));
    }
}
