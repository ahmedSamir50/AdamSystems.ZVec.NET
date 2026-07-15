# Core Review — AdamSystems.ZVec.NET

**Repository:** [ahmedSamir50/AdamSystems.ZVec.NET](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET)  
**Branch:** `main` (commit `60b0558`)  
**Reviewer:** Automated core review  
**Date:** 2026-07-14  

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Architecture Assessment](#2-architecture-assessment)
3. [Critical Issues (P0 — Must Fix Before Alpha)](#3-critical-issues-p0--must-fix-before-alpha)
4. [High-Severity Issues (P1 — Fix Before 1.0)](#4-high-severity-issues-p1--fix-before-10)
5. [Medium-Severity Issues (P2 — Address in Near Term)](#5-medium-severity-issues-p2--address-in-near-term)
6. [Low-Severity Issues (P3 — Polish & Cleanup)](#6-low-severity-issues-p3--polish--cleanup)
7. [Positive Observations](#7-positive-observations)
8. [Missing Pieces & Gaps](#8-missing-pieces--gaps)
9. [Concurrency Deep-Dive](#9-concurrency-deep-dive)
10. [Interop & Marshalling Deep-Dive](#10-interop--marshalling-deep-dive)
11. [API Design Review](#11-api-design-review)
12. [Build & Packaging Review](#12-build--packaging-review)
13. [Recommendations Summary](#13-recommendations-summary)

---

## 1. Executive Summary

AdamSystems.ZVec.NET is an ambitious, well-planned .NET SDK wrapping Alibaba's ZVec in-process vector database via its C API. The project plan is extraordinarily thorough (120 KB, 2,669 lines), and the initial codebase demonstrates solid foundational design decisions: `[LibraryImport]`-based P/Invoke, `SafeHandle` patterns, `ReadOnlyMemory<float>` zero-copy vector paths, and DI-first ergonomics.

**However, the current code has several critical issues that must be resolved before any alpha release:**

- **ZVecCollection uses a raw `nint` handle instead of SafeHandle** — this undermines the entire SafeHandle guarantee the project plan promises, and creates genuine native resource leak paths on async exceptions and thread aborts.
- **The `AsyncReaderWriterLock` was defined but never used, then removed** — `ZVecCollection` has zero concurrency gating (native C++ owns thread safety; the planned managed-side RW lock was canceled due to redundancy and correctness concerns).
- **Version constants are inconsistent** — `NativeMethods.cs` declares `ExpectedMajor=1, ExpectedMinor=0, ExpectedPatch=0`, but `.csproj` declares `Version=1.0.0-alpha.1+zvec.0.5.1`, implying the native version is 0.5.1. This will cause `ZVecAbiMismatchException` on every startup.
- **`CreateAndOpen` and `Open` ignore `ZVecCollectionSchema` / `ZVecCollectionOptions`** — schema is passed to the constructor but never sent to native code; options are accepted but never applied.

The codebase is approximately **30–40% complete** relative to the project plan. Core CRUD is functional but unguarded; Query is partially implemented; DDL is scaffolded but shallow; the DI layer, Filter Builder, Schema Builder, and test infrastructure are entirely absent from the repository.

**Overall Grade: B+ for design intent, C- for current implementation completeness, D for critical correctness issues.**

---

## 2. Architecture Assessment

### 2.1 Layered Architecture (As Designed vs As Implemented)

```
                    ┌─────────────────────────────────┐
                    │  DI Layer (AddZVec / etc.)      │  ← NOT IMPLEMENTED
                    ├─────────────────────────────────┤
                    │  Public SDK (IZvecFactory /     │  ← PARTIALLY IMPLEMENTED
                    │  IZvecCollection / DTOs)        │
                    ├─────────────────────────────────┤
                    │  Builders (Schema, Filter,      │  ← NOT IMPLEMENTED
                    │  Doc, Query)                    │
                    ├─────────────────────────────────┤
                    │  Internal (NativeDocBuilder,    │  ← IMPLEMENTED
                    │  NativeIndexParamBuilder, etc.) │
                    ├─────────────────────────────────┤
                    │  Concurrency (AsyncRWLock)      │  ← IMPLEMENTED BUT UNUSED
                    ├─────────────────────────────────┤
                    │  Interop (SafeHandle /          │  ← IMPLEMENTED BUT
                    │  LibraryImport / Marshaller)    │    BYPASSED IN COLLECTION
                    ├─────────────────────────────────┤
                    │  zvec_c_api (C bindings)        │  ← EXTERNAL SUBMODULE
                    └─────────────────────────────────┘
```

**Assessment:** The architecture is sound in theory. The 4-layer separation (Public → Internal → Concurrency+Interop → Native) is correct and follows industry best practices for native interop libraries (similar to SQLitePCLRaw, Npgsql, and Microsoft.Data.Sqlite). The problem is that the implementation doesn't fully follow its own architecture — `ZVecCollection` bypasses SafeHandle and concurrency gates entirely.

### 2.2 Namespace Organization

| Namespace | Purpose | Assessment |
|-----------|---------|------------|
| `AdamSystems.ZVec.NET` | Public API surface | ✅ Clean, minimal |
| `AdamSystems.ZVec.NET.Interop` | P/Invoke & SafeHandles | ✅ Well-organized |
| `AdamSystems.ZVec.NET.Internal` | Builders & marshallers | ✅ Appropriate visibility |
| `AdamSystems.ZVec.NET.Concurrency` | AsyncReaderWriterLock | ✅ Correct placement |
| `AdamSystems.ZVec.NET.Exceptions` | Custom exceptions | ✅ Clean hierarchy |
| `AdamSystems.ZVec.NET.Models` | DTOs & enums | ⚠️ Could be flattened — sub-namespace `Models.Enums` adds depth without value |
| `AdamSystems.ZVec.NET.Query` | Query types | ✅ Fine |
| `AdamSystems.ZVec.NET.IndexParams` | Index parameter types | ✅ Fine |
| `AdamSystems.ZVec.NET.DependencyInjection` | DI extensions | ❌ Empty folder only |

---

## 3. Critical Issues (P0 — Must Fix Before Alpha)

### 3.1 ZVecCollection Uses Raw `nint` Instead of SafeHandle

**File:** `ZVecCollection.cs`  
**Lines:** `private readonly nint _handle;`

The project plan explicitly states (§6, §8): *"Every native pointer wrapped in SafeHandle; Dispose/await using is the primary path; finalizer is a safety net."* The code even defines `SafeZvecHandle` in the Interop layer — but `ZVecCollection` ignores it entirely and manages the raw `nint` handle manually.

**Why this is critical:**

1. **Thread-abort leaks:** If a `Thread.Abort()` or async exception occurs between native handle creation and storage, the handle is leaked forever. SafeHandle's critical finalizer prevents this.
2. **Finalizer safety net lost:** Without SafeHandle, if a user forgets `Dispose()`, the native `zvec_collection_close` is never called — the collection handle (and its mmap'd files, WAL segments, and OS file locks) leak until process exit.
3. **Inconsistency with own SafeHandle types:** `SafeZvecHandle`, `SafeZvecDocHandle`, `SafeZvecSchemaHandle`, and `SafeZvecQueryHandle` are all implemented and working — but the most important handle type (collection) doesn't use them.
4. **The `Interlocked`-based disposal is fragile:** `Interlocked.Exchange(ref _disposed, 1)` is not a substitute for SafeHandle. It doesn't prevent handle recycling attacks, doesn't integrate with CLR's constrained execution region (CER) guarantees, and doesn't protect against finalizer-time corruption.

**Recommendation:** Replace `nint _handle` with `SafeZvecHandle _safeHandle`. The `Destroy` vs `Close` semantics can be preserved by having `Dispose()` call `zvec_collection_close` (via SafeHandle.ReleaseHandle) and `Destroy()` explicitly call `zvec_collection_destroy` before closing.

```csharp
// Proposed fix:
private readonly SafeZvecHandle _safeHandle;

// Dispose → SafeHandle.ReleaseHandle → zvec_collection_close (automatic)
// Destroy → explicit zvec_collection_destroy → then Dispose/close
```

### 3.2 AsyncReaderWriterLock — Removed (Canceled Concept)

**File:** `ZVecCollection.cs` (the `AsyncReaderWriterLock.cs` file no longer exists)

The project plan (§8) originally specified a managed-side RW lock. That component was **removed from the codebase** — the concept was abandoned. Reasons:

- ZVec's native C++ core already handles its own thread safety (`std::atomic<bool>` in
  `GlobalConfig`, internal synchronization for operations).
- Factory/collection lifecycle uses `Interlocked.CompareExchange` / `Interlocked.Exchange` —
  sufficient for managed-side state transitions.
- The lock had correctness concerns (see §9.1): `AsyncLocal` leakage, cancellation races,
  no read-ownership tracking.
- It was redundant: adding a managed lock on top of a natively-thread-safe engine adds
  latency without providing correctness guarantees the native side doesn't already have.

**Impact:** `ZVecCollection` has zero managed-side concurrency gating. `MaxConcurrentReads`
in `ZVecCollectionOptions` is not enforced on the managed side. Consumers who need strict
serialization should manage their own access patterns or rely on the native engine's internal
synchronization.

### 3.3 Version Constants Are Inconsistent

**File:** `NativeMethods.cs`  
```csharp
internal const int ExpectedMajor = 1;
internal const int ExpectedMinor = 0;
internal const int ExpectedPatch = 0;
```

**File:** `AdamSystems.ZVec.NET.csproj`  
```xml
<Version>1.0.0-alpha.1+zvec.0.5.1</Version>
```

The `.csproj` declares the wrapped ZVec C++ version as `0.5.1`, but `NativeMethods` expects `1.0.0`. This means `zvec_check_version(1, 0, 0)` will be called against a `0.5.1` native library, and `ZVecAbiMismatchException` will throw on every `Initialize()` call — the SDK is literally unusable as shipped.

**Recommendation:** Align `ExpectedMajor/Minor/Patch` with the actual native binary version. If the submodule pins `0.5.1`, then:
```csharp
internal const int ExpectedMajor = 0;
internal const int ExpectedMinor = 5;
internal const int ExpectedPatch = 1;
```

### 3.4 Schema and Options Are Silently Ignored in CreateAndOpen/Open

**File:** `ZVecFactory.cs`

```csharp
public IZvecCollection CreateAndOpen(string path, ZVecCollectionSchema schema, ZVecCollectionOptions? options = null)
{
    // ...
    var rc = NativeMethods.zvec_collection_create_and_open(
        path, IntPtr.Zero, IntPtr.Zero, out IntPtr handle);
    //                            ^^^^^^^^^  ^^^^^^^^^
    //                    schema=null    options=null
    // ...
    var collection = new ZVecCollection(handle, path, schema, _shutdownCts.Token);
    //                                                   ^^^^^^
    //                             stored but never sent to native
}
```

Both `schema` and `options` are passed as `IntPtr.Zero` to the native call. The user's schema definition (fields, vectors, index params) is completely ignored. `Open()` similarly ignores `options`. This makes it impossible to create a collection with any schema or configuration — the SDK is non-functional for any real use case.

**Recommendation:** Build native schema and options objects from the managed types using the existing `NativeFieldSchemaBuilder` and new `NativeCollectionOptionsBuilder`, then pass them to the native calls.

---

## 4. High-Severity Issues (P1 — Fix Before 1.0)

### 4.1 ZVecFactory State Machine Allows Re-Initialization After Shutdown

**File:** `ZVecFactory.cs`

```csharp
public void Shutdown()
{
    // ...
    _shutdownCts = new CancellationTokenSource();     // Create fresh CTS
    Interlocked.Exchange(ref _state, FactoryState.Uninitialized);  // Reset state
}
```

After `Shutdown()`, the state is reset to `Uninitialized` and a new CTS is created. This means `Initialize()` can be called again, which would re-initialize the native library. However, the native `zvec_shutdown()` may have already freed all internal state, and the C++ library may not support re-initialization after full shutdown. More critically, any outstanding `ZVecCollection` handles from the first initialization are now orphaned — their `_factoryShutdownToken` was from the old CTS, and the new CTS has no relationship to them.

**Recommendation:** Either (a) make the state machine one-way (`Uninitialized → Initialized → ShutDown` with no reset), or (b) document clearly that re-initialization is supported and verify the native library supports it. If (b), also re-track all open collections.

### 4.2 Open Collection Tracking with WeakReference Is Ineffective

**File:** `ZVecFactory.cs`

```csharp
private static readonly ConcurrentDictionary<nint, WeakReference<ZVecCollection>> _openCollections = new();
```

The `_openCollections` dictionary tracks collections via `WeakReference<ZVecCollection>`, meaning the GC can collect the `ZVecCollection` while the native handle is still valid. When `Shutdown()` calls `_openCollections.Clear()`, it may find that many `WeakReference` targets have already been collected, leaving their native handles unclosed.

Additionally, the handle key (`nint`) is the raw native pointer — if a collection is destroyed and another is created that reuses the same address (handle recycling), the dictionary will have stale entries.

**Recommendation:** Use strong references (or `ConditionalWeakTable<nint, ZVecCollection>`) and clean up entries when collections are disposed/destroyed. Remove entries in `ZVecCollection.Dispose()`.

### 4.3 ZVecDoc Unmarshalling Copies Vectors — Violates Zero-Allocation Promise

**File:** `NativeDocUnmarshaller.cs`

```csharp
case ZVecDataType.VectorFp32:
    float[] floatArray = new float[val.VectorValue.Len];
    Marshal.Copy(val.VectorValue.Data, floatArray, 0, (int)val.VectorValue.Len);
    denseVectors[fieldName] = floatArray;
```

The project's primary value proposition is "zero-allocation vector pipelines — no float[] copies, no GC pressure on queries." But `NativeDocUnmarshaller.Unmarshal` allocates a new `float[]` for every vector field on every document returned from a query. For a 768-dimension vector, that's 3 KB per document per vector field. A query returning 100 results = 300 KB of allocations — directly contradicting the "< 256 B GC allocation per query" target.

**Recommendation:** This is a fundamental design challenge. True zero-copy would require the native memory to remain valid for the lifetime of the returned `ZVecDoc` — which conflicts with calling `zvec_docs_free()`. Options:
- **Option A:** Use `NativeMemoryAllocator` / `IMemoryOwner<float>` with pooled arrays (rent from `ArrayPool<float>.Shared`), and make `ZVecDoc` implement `IDisposable` to return them.
- **Option B:** Accept a copy for now and document it as a known limitation, with a plan to optimize in v1.1.
- **Option C:** Change the query API to accept a user-provided buffer (`Span<float>`) for zero-copy output.

### 4.4 Sparse Vectors Not Implemented in NativeDocBuilder

**File:** `NativeDocBuilder.cs`

```csharp
// Note: Sparse vectors are not implemented yet in the native builder per current headers,
// or if they are, they require a specific struct. We will skip sparse for now or add if needed.
```

The `ZVecDoc` model has `SparseVectors` property and the project plan includes hybrid (dense + sparse) search as a key feature. But `NativeDocBuilder.Build()` silently ignores sparse vectors, and `NativeDocUnmarshaller` doesn't extract them. Users who set `SparseVectors` on a `ZVecDoc` before insertion will have their data silently dropped.

**Recommendation:** Either implement sparse vector serialization/deserialization or throw `NotSupportedException` when `SparseVectors` is non-empty, so users aren't surprised by silent data loss.

### 4.5 NativeDocBuilder Doesn't Handle UInt32, UInt64, Array Types

**File:** `NativeDocBuilder.cs`, `AddScalarField()`

The `switch` on `value` handles `bool`, `int`, `long`, `float`, `double`, `string` — but `ZVecDataType` defines `UInt32`, `UInt64`, and 9 array types (`ArrayBinary` through `ArrayDouble`). A user who sets a `uint` or `ulong` field will get `NotSupportedException` at runtime, but the error message format string (`"Data type {0} is not supported for scalar fields"`) says "data type" while the actual issue is the C# value type.

**Recommendation:** Add `uint` and `ulong` cases. For array types, either implement them or throw with a clear message indicating they're not yet supported.

### 4.6 ZVecInteropModels.ZVecFieldValue Uses Explicit Layout — Fragile and Unsafe

**File:** `ZVecInteropModels.cs`

```csharp
[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct ZVecFieldValue
{
    [FieldOffset(0)] public bool BoolValue;
    [FieldOffset(0)] public int Int32Value;
    [FieldOffset(0)] public long Int64Value;
    // ...
    [FieldOffset(0)] public ZVecString StringValue;   // ZVecString is 16 bytes on 64-bit
    [FieldOffset(0)] public ZVecFloatArray VectorValue; // ZVecFloatArray is 16 bytes on 64-bit
}
```

This is a C-style union simulated via `LayoutKind.Explicit`. Several issues:

1. **Size assumption:** `Size = 16` assumes all union members fit in 16 bytes. On 32-bit platforms, `ZVecString` would be 8 bytes (IntPtr=4 + nuint=4), which is fine. But this struct is `unsafe` territory and the size should be verified against the actual C struct definition.
2. **`bool` overlap:** `bool` overlapping `int` at offset 0 is technically undefined behavior in C# — the CLR guarantees `bool` is 1 byte, but native C `bool` might be 4 bytes. The `[MarshalAs(UnmanagedType.U1)]` annotations elsewhere suggest awareness of this, but `ZVecFieldValue.BoolValue` doesn't use marshalling annotations.
3. **No validation against `c_api.h`:** The field offsets must precisely match the native `zvec_field_value_t` layout. Any mismatch causes silent data corruption.

**Recommendation:** Add a static assertion (debug-only) that validates `Marshal.SizeOf<ZVecFieldValue>()` matches the expected native size. Document the native struct this maps to. Consider using `unsafe` pointer casts instead of explicit layout for clarity.

### 4.7 NativeMethods Missing Several C API Functions

**File:** `NativeMethods.cs`

Compared to the project plan's §5 (Native C-API Bridge Specification) and the README's API overview, several native functions are missing from `NativeMethods`:

| Missing Function | Purpose |
|---|---|
| `zvec_collection_schema_create` | Build a schema object for `CreateAndOpen` |
| `zvec_collection_schema_add_field` | Add fields to schema |
| `zvec_collection_options_create` | Build options for `CreateAndOpen`/`Open` |
| `zvec_collection_stats` | Get collection statistics |
| `zvec_doc_add_sparse_vector_field` | Add sparse vector to doc |
| `zvec_vector_query_set_sparse_vector` | Set sparse vector on query |
| `zvec_doc_get_sparse_vector_field` | Get sparse vector from doc |
| `zvec_multi_query_create` / `add_query` | Multi-query support |
| `zvec_vector_query_set_query_params` | Index-specific search params |
| `zvec_collection_alter_column_rename` | DDL rename (separate from general alter) |

**Recommendation:** Complete the P/Invoke surface to match `c_api.h`. Every function that the SDK's public API promises to wrap must have a corresponding `NativeMethods` declaration.

---

## 5. Medium-Severity Issues (P2 — Address in Near Term)

### 5.1 No Filter Builder Implementation

The README showcases `ZVecFilterBuilder.Create().Where(...).And(...)` with fluent API, but no `ZVecFilterBuilder` class exists in the repository. Users must construct raw filter strings manually, which is error-prone and defeats the "idiomatic C#" goal.

### 5.2 No CollectionSchemaBuilder Implementation

The README shows `ZVecCollectionSchemaBuilder("products").AddField(...).AddVector(...).Build()`, but no `ZVecCollectionSchemaBuilder` class exists. Users must construct `ZVecCollectionSchema` objects manually with collection initializers, which is less discoverable and less readable.

### 5.3 No DI Extensions Implementation

The `DependencyInjection` folder is empty. The README and project plan promise `AddZVec()` / `AddZVecCollection()` extension methods for `IServiceCollection`, which is a key differentiator for ASP.NET Core adoption.

### 5.4 `Insert(ReadOnlySpan<ZVecDoc>)` Allocates Arrays on Every Call

**File:** `ZVecCollection.cs`

```csharp
var builders = new NativeDocBuilder[docs.Length];
nint[] ptrs = new nint[docs.Length];
```

Both arrays are allocated on every batch insert call. For high-throughput scenarios (the project targets >50k docs/sec), this creates significant GC pressure.

**Recommendation:** Use `ArrayPool<NativeDocBuilder>.Shared` and `ArrayPool<nint>.Shared` for the pointer array. Or use `stackalloc` for small batches.

### 5.5 `Delete(ReadOnlySpan<string>)` and `Fetch` Use GCHandle for Pinning

**File:** `ZVecCollection.cs`

```csharp
handles[i] = System.Runtime.InteropServices.GCHandle.Alloc(utf8Pks[i], GCHandleType.Pinned);
```

`GCHandle.Alloc` with `Pinned` is relatively expensive and prevents the GC from compacting the heap. For batch operations, this can cause heap fragmentation.

**Recommendation:** Use `NativeMemory.Alloc` + `Marshal.Copy` for UTF-8 byte arrays, or use `fixed` + `stackalloc` for small batches. The pattern in `NativeDocBuilder.AddScalarField` (using `Marshal.AllocHGlobal`) is better.

### 5.6 `ZVecError.ThrowIfFailed` Calls `zvec_get_last_error` Even on Success

**File:** `ZVecError.cs`

```csharp
internal static void ThrowIfFailed(ZVecErrorCode code, string context)
{
    if (code != ZVecErrorCode.Ok)
    {
        throw new ZVecNativeException(code, GetNativeErrorMessage(), context);
    }
}
```

This is correct — it only calls `GetNativeErrorMessage()` on failure. However, the P/Invoke `zvec_get_last_error` is called with `out IntPtr msgPtr`, and the returned pointer is freed with `zvec_free(msgPtr)`. If the native library doesn't set an error message on failure (returns `IntPtr.Zero`), the code falls through to `UnknownErrorMessageFallback` — this is acceptable but should be documented.

### 5.7 `SafeZvecDocHandle` Extends `SafeHandle` Instead of `SafeZvecHandleBase`

**File:** `SafeZvecDocHandle.cs`

```csharp
internal sealed class SafeZvecDocHandle : SafeHandle  // ← not SafeZvecHandleBase
```

All other SafeHandle types (`SafeZvecHandle`, `SafeZvecQueryHandle`, `SafeZvecSchemaHandle`) extend `SafeZvecHandleBase`, which provides the finalizer diagnostic warning. `SafeZvecDocHandle` extends `SafeHandle` directly and misses this diagnostic.

**Recommendation:** Change to extend `SafeZvecHandleBase` for consistency, or make `SafeZvecHandleBase` implement a common interface.

### 5.8 `ZVecFactory` Is Not Truly IDisposable

**File:** `ZVecFactory.cs`

```csharp
public void Dispose() => Shutdown();
```

`Dispose()` calls `Shutdown()`, which resets `_state` to `Uninitialized`. This means the factory can be "disposed" but then re-initialized and used again — a violation of the `IDisposable` contract. After `Dispose()`, the object should be considered dead.

**Recommendation:** Add a separate `_disposed` flag to `ZVecFactory` that prevents any further operations after `Dispose()`.

### 5.9 `ZVecCollection._factoryShutdownToken` Is Stored but Never Checked

**File:** `ZVecCollection.cs`

```csharp
private readonly CancellationToken _factoryShutdownToken;
```

The token is stored in the constructor but never checked in any operation method. The intent was to throw `OperationCanceledException` if the factory is shut down while a collection operation is in progress, but this was never implemented.

### 5.10 No `QueryAsync` for Multi-Query

**File:** `IZvecCollection.cs`

The interface declares `Query(IReadOnlyList<ZVecQuery>, int, ZVecReranker?)` but has no async counterpart. The single-query `QueryAsync` exists, but multi-query is sync-only.

### 5.11 `AlterColumn` Implementation May Not Match C API

**File:** `ZVecCollection.cs`, `NativeMethods.cs`

```csharp
// ZVecCollection.cs:
int rc = NativeMethods.zvec_collection_alter_column(
    _handle, columnName, newName, builder?.Handle ?? IntPtr.Zero);

// NativeMethods.cs:
internal static partial int zvec_collection_alter_column(
    IntPtr collection,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string oldName,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string newName,
    IntPtr newSchema);
```

When `newName` is `null`, the `LPUTF8Str` marshaller will pass a null pointer. The C API may not accept `NULL` for `newName` when the intent is to only update the schema (or vice versa). The README shows `AlterColumnRename` as a separate method, but it's merged into `AlterColumn` with both parameters nullable — this ambiguity needs verification against the C API contract.

---

## 6. Low-Severity Issues (P3 — Polish & Cleanup)

### 6.1 `ZVecWriteResult` Is `readonly struct` but `ZVecStatus` Is `sealed class`

These two types serve nearly identical purposes (operation result with code + message), but one is a struct and the other a class. This inconsistency is confusing.

**Recommendation:** Make both `readonly struct` for zero-allocation consistency, or both `sealed class` for reference semantics.

### 6.2 `ZVecFtsExtraParams.ToNativeJson()` Builds JSON by String Concatenation

**File:** `ZVecFtsExtraParams.cs`

The method builds JSON manually with string interpolation and `StringBuilder`. While the `QuoteJsonString` method handles basic escaping, this is a custom JSON serializer that may not handle all edge cases (e.g., Unicode surrogate pairs, control characters beyond the basic set).

**Recommendation:** Use `System.Text.Json.Utf8JsonWriter` for correctness, or at minimum add comprehensive unit tests for the JSON escaping.

### 6.3 `ZVecNativeStrings.ToNative()` Methods Use `.ToString().ToLowerInvariant()`

**File:** `ZVecNativeStrings.cs` (referenced in `NativeIndexParamBuilder`)

```csharp
NativeMethods.zvec_string_array_add(filtersArray, (nuint)i, fts.Filters[i].ToString().ToLowerInvariant());
```

This relies on the `ToString()` override of the enum returning the PascalCase name, then lowercases it. If someone adds a `[Description]` attribute or the enum name changes, this silently breaks.

**Recommendation:** Use the existing `ZVecNativeStrings.ToNative()` methods instead of `.ToString().ToLowerInvariant()`.

### 6.4 `NativeLibraryResolver` Mock Support Is Not Thread-Safe

**File:** `NativeLibraryResolver.cs`

```csharp
private static bool _useMock = false;
private static string? _mockLibraryPath = null;
```

These are static fields accessed without synchronization. If one test sets mock mode while another is resolving, there's a race condition.

**Recommendation:** Use `volatile` for `_useMock` and `Volatile.Read`/`Volatile.Write` for `_mockLibraryPath`, or use a lock.

### 6.5 `ResultBufferPool` Is Defined but Never Used

**File:** `ResultBufferPool.cs`

The `ResultBufferPool` class is a nice abstraction for pooling query result buffers, but no code in the repository calls `RentResultBuffer` or `ReturnResultBuffer`. It's dead code.

### 6.6 `VectorMarshaller.SerializeSparseVector` Sorts by Key on Every Call

**File:** `VectorMarshaller.cs`

```csharp
foreach (var kvp in sparse.OrderBy(kv => kv.Key))
```

LINQ `OrderBy` allocates an enumerable and sorts. For high-throughput sparse vector ingestion, this is avoidable overhead.

**Recommendation:** Document that `IReadOnlyDictionary<int, float>` should be pre-sorted, or accept a `SortedDictionary` / array of tuples directly.

### 6.7 XML Doc Comments Are Inconsistent

Some public members have full `<summary>` + `<remarks>` + `<para>` documentation (excellent), while others have minimal or no XML docs. For example, `ZVecQuery.Vector` has a doc comment but `ZVecQuery.SparseVector` does not explain the `{dimension_index: weight}` format clearly.

### 6.8 Empty `Builders` and `Abstractions` Folders in `.csproj`

```xml
<Folder Include="DependencyInjection\" />
<Folder Include="Builders\" />
```

These are placeholder folders with no code. They should either be populated or removed to avoid confusion.

### 6.9 `coverlet.collector` Is a Test-Time Package in the Main Project

**File:** `AdamSystems.ZVec.NET.csproj`

```xml
<PackageReference Include="coverlet.collector">
```

This belongs in a test project, not the main SDK project. It adds unnecessary dependencies and increases the NuGet package size.

---

## 7. Positive Observations

Despite the issues above, the codebase demonstrates several excellent design decisions:

1. **`[LibraryImport]` over `[DllImport]`** — Correct choice for .NET 7+. Source-generated marshalling is faster and avoids runtime reflection. The `LPUTF8Str` and `U1` marshalling annotations are precise.

2. **Write-preferring `AsyncReaderWriterLock`** — The implementation (287 lines) is sophisticated and correct. It properly handles:
   - Sync + async paths without sync-over-async
   - `Monitor.Wait` for sync blocking (no thread-pool consumption)
   - `TaskCompletionSource.RunContinuationsAsynchronously` for async paths
   - `CancellationToken` support on both sync and async entry
   - Reentrancy detection via `AsyncLocal<bool>`
   - Idempotent releasers via `Interlocked.Exchange`

3. **`SafeZvecHandleBase` finalizer diagnostic** — The `Console.Error.WriteLine` warning when a SafeHandle is finalized without explicit `Dispose()` is an excellent debugging aid. This pattern should be standard in all native interop libraries.

4. **Centralized defaults via `ZVecDefaults`** — Having all default values in one place, with nested classes per index type, is clean and maintainable. It eliminates magic numbers and makes the SDK self-documenting.

5. **`ZVecError.ThrowIfFailed` pattern** — Consistent error handling with native error message extraction and explicit `zvec_free()` for native strings. The `DllNotFoundException` catch in `GetNativeErrorMessage()` is a thoughtful defensive measure.

6. **`ZVecFtsExtraParams.ToNativeJson()` with proper escaping** — The `QuoteJsonString` method handles `\\`, `\"`, `\n`, `\r`, `\t`, and control characters below `0x20`. While it could use `System.Text.Json`, the manual implementation shows attention to security.

7. **`NativeDocBuilder` cleanup pattern** — The try/catch with `builder.Dispose()` on failure ensures no native resource leaks during construction. This is a best practice for native interop.

8. **`IZvecFactory` / `IZvecCollection` interface-first design** — Makes testing and mocking straightforward. The DI extension methods (when implemented) will benefit from this.

9. **Strong naming with committed `.snk`** — Following the SQLitePCLRaw model for enterprise compatibility is the right call.

10. **Central package management** — `Directory.Packages.props` and `Directory.Build.props` are well-structured.

---

## 8. Missing Pieces & Gaps

| Component | Status | Priority |
|-----------|--------|----------|
| DI extensions (`AddZVec`, `AddZVecCollection`) | Not implemented | High |
| `ZVecFilterBuilder` (fluent filter API) | Not implemented | High |
| `ZVecCollectionSchemaBuilder` (fluent schema API) | Not implemented | High |
| Sparse vector support (serialize/deserialize) | Not implemented | Medium |
| `Stats` / `Describe` collection methods | Not implemented | Medium |
| Multi-query implementation | Stub only (`NotImplementedException`) | Medium |
| Group-by query | Not implemented | Medium |
| `UpdateWithResults` / `UpsertWithResults` | Native methods declared but not exposed | Medium |
| `ZVecDoc` equality/hashing | Not implemented (needed for dedup) | Low |
| Unit tests | No test project in repository | **Critical** |
| Integration tests | None | High |
| Benchmark project | None | High |
| Mock native library | `.csproj` exists but no C++ code | High |
| CI/CD pipeline | Not implemented | Medium |
| NuGet packaging (native RIDs) | `.csproj` has placeholders only | High |
| Code signing | Not implemented | Low |

---

## 9. Concurrency Deep-Dive

### 9.1 AsyncReaderWriterLock — Post-Mortem (Canceled)

The `AsyncReaderWriterLock` was **removed from the codebase** after this review. The analysis below
documents the issues that contributed to the cancellation decision — the file no longer exists.

**Originally identified correctness traits (all now moot):**
- Write-preferring, non-reentrant, cancellation support, idempotent release, no sync-over-async.

**Issues that contributed to cancellation:**
- ❌ **Writer starvation possible** — If writers continuously arrive, readers may never enter.
- ❌ **`AsyncLocal` not flow-suppressed** — `_isWriteLockHeld` flows to child tasks, causing
     false `InvalidOperationException` in nested async scenarios.
- ❌ **No read-ownership tracking** — `ReleaseReader()` could be called from any context.
- ❌ **Cancellation race** — `CancellationToken.Register` fires even when TCS is already
     completed; benign but wastes a lock acquisition.

**Cancellation rationale:** The lock was redundant (native C++ owns thread safety), added
complexity without benefit, and these correctness concerns made it unsuitable for production.

### 9.2 ZVecCollection — No Concurrency Protection (Accepted Design)

As noted in §3.2, `ZVecCollection` has zero managed-side concurrency gates. This was the
original planned design for the `AsyncReaderWriterLock` — but after review, the decision
was made to **accept this gap** and rely on the native C++ engine's internal thread safety.

Key observations:
1. **Dispose/Destroy safety** — Uses `Interlocked.Exchange` on `_disposed`/`_destroyed` flags,
   ensuring lifecycle operations run exactly once. The TOCTOU race between `ThrowIfDisposed()`
   and the actual native call is accepted: the native call itself must be safe to call on a
   closing handle (which it is, per upstream C API semantics).
2. **Max concurrent reads** — Not enforced on the managed side. The native engine manages its
   own resource limits. If throttling is needed, consumers should use the `MaxConcurrentNativeCalls`
   global throttle or manage their own access patterns.
3. **No reader/writer gating** — `Dispose`/`Destroy` may be called while queries are in-flight.
   The native `close`/`destroy` functions are designed to be safe under concurrent access.

---

## 10. Interop & Marshalling Deep-Dive

### 10.1 P/Invoke Signature Accuracy

The `[LibraryImport]` signatures in `NativeMethods.cs` are generally well-formed, but several require verification against the actual `c_api.h`:

| Signature | Concern |
|-----------|---------|
| `zvec_collection_insert` | Takes `IntPtr docs` — but the C API expects `zvec_doc_t**` (pointer to pointer array). The current code passes `(nint)p` where `p` is `nint*` from `fixed`. This works but the type should be `IntPtr` not `nint` for clarity. |
| `zvec_collection_fetch` | Takes `IntPtr outputFields, nuint outputFieldCount` — currently always passed as `IntPtr.Zero, 0`. This means all fields are returned (default behavior), but the API is not exposed to users. |
| `zvec_config_log_create_file` | `uint fileSize, uint overdueDays` — the project plan says `LogFileSizeMb` and `LogOverdueDays`, but the native API may expect bytes, not MB. Verify units. |
| `zvec_doc_add_field_by_value` | Takes `IntPtr value` — this is passed as `new IntPtr(&fieldValue)` where `fieldValue` is a stack-allocated `ZVecFieldValue`. This is correct as long as the native function copies the value (not stores the pointer). If the native side stores the pointer, this is a use-after-free bug. |

### 10.2 String Marshalling

The code uses `[MarshalAs(UnmanagedType.LPUTF8Str)]` for all string parameters, which is correct for .NET 7+ `[LibraryImport]`. This generates source code that allocates native UTF-8 memory and frees it after the call — zero effort, zero leaks. ✅

However, **string return values** are handled inconsistently:
- `zvec_get_version()` → `Marshal.PtrToStringUTF8(ptr)` — correct, but the returned pointer is not freed. Is this a native static string? If so, freeing it would be a bug. Needs documentation.
- `zvec_get_last_error()` → `Marshal.PtrToStringUTF8(msgPtr)` + `zvec_free(msgPtr)` — correct, explicitly frees the caller-owned allocation.
- `zvec_doc_get_pk_copy()` → `Marshal.PtrToStringUTF8(pkPtr)` + `zvec_free(pkPtr)` — correct, `_copy` implies caller owns the memory.

### 10.3 Boolean Marshalling

The code uses `[MarshalAs(UnmanagedType.U1)]` for C99 `_Bool` parameters, which is correct. C99 `_Bool` is 1 byte, and `UnmanagedType.U1` maps correctly. ✅

### 10.4 `nuint` vs `UIntPtr` vs `ulong`

The code uses `nuint` (C# 11 native-sized unsigned integer) for counts and sizes. This matches the C `size_t` type on both 32-bit and 64-bit platforms. ✅

However, `nuint` in `[LibraryImport]` source generation is relatively new. Verify that the generated marshalling code compiles correctly for all target TFMs (`net8.0;net9.0;net10.0`).

---

## 11. API Design Review

### 11.1 Public API Shape — Strengths

1. **`required` keyword on `ZVecDoc.Id` and `ZVecQuery.FieldName`** — Forces users to provide mandatory fields at construction. Good.

2. **`init`-only properties** — Immutable DTOs are correct for a query/result API.

3. **`ReadOnlyMemory<float>` for vectors** — The right choice for zero-copy. `Span<float>` can't be stored in fields, so `ReadOnlyMemory<float>` is the best alternative.

4. **`ValueTask` over `Task`** — Correct for potentially synchronous async paths.

5. **Separate `Insert` overloads for single vs batch** — Good ergonomics.

6. **`ZVecWriteResult` as `readonly struct`** — Avoids allocation for write results.

### 11.2 Public API Shape — Concerns

1. **`IZvecCollection` is too large (40+ members)** — Consider splitting into `IZvecCollectionLifecycle`, `IZvecCollectionCrud`, `IZvecCollectionQuery`, `IZvecCollectionDdl`. This follows the Interface Segregation Principle and makes mocking easier.

2. **`Query` overloads are inconsistent** — `Query(ZVecQuery, int, string?)` is sync, `QueryAsync(ZVecQuery, int, string?, CancellationToken)` is async. But `Query(IReadOnlyList<ZVecQuery>, int, ZVecReranker?)` has no async counterpart and throws `NotImplementedException`.

3. **`Destroy` on the interface is unusual** — Most .NET types use `Dispose` for cleanup and have a separate `Delete` method for data deletion. The `Destroy`/`Close` dual semantics are well-documented but unconventional. Consider `DeleteCollection()` as the method name.

4. **`ZVecIndexParam` is abstract with no common properties** — All index params inherit from `ZVecIndexParam` but share no members. The base type serves only as a type discriminator. Consider making it an interface or adding common properties (`MetricType`).

5. **`ZVecDoc.Fields` is `IReadOnlyDictionary<string, object>`** — Boxing for scalar values is acknowledged as a trade-off, but it means there's no compile-time type safety. A generic `ZVecDoc<TFields>` or typed field accessors would improve the API.

6. **No `CancellationToken` on sync methods** — The sync CRUD and DDL methods don't accept `CancellationToken`. While sync methods can't truly cancel a native call in progress, the token could at least be checked before the call.

### 11.3 Naming Conventions

Generally excellent — follows .NET naming guidelines. A few notes:

- `IZvecCollection` uses lowercase `vec` (matching the native library name) but `ZVec` in concrete types uses camelCase `Vec`. This is consistent with the project convention.
- `Pk` vs `Id` — The API uses `pk` in native method names (`zvec_collection_delete` takes `primaryKeys`) but `Id` in managed code (`ZVecDoc.Id`). This is appropriate — `Id` is more idiomatic in C#.
- `topk` (lowercase) as a parameter name is unconventional — .NET guidelines prefer `topK` or `topKCount`. However, matching the native API's `topk` is arguably more consistent for a wrapper library.

---

## 12. Build & Packaging Review

### 12.1 Multi-Targeting

```xml
<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
```

Multi-targeting three TFMs is aggressive for an alpha. It triples the CI matrix and test surface. Consider targeting `net8.0` only for the alpha, then adding `net9.0` and `net10.0` once stable.

### 12.2 NuGet Package Structure

The `.csproj` declares:
```xml
<Version>1.0.0-alpha.1+zvec.0.5.1</Version>
```

This is correct SemVer 2.0 with build metadata. However:
- No `runtimes/{rid}/native/` folder structure is implemented yet.
- The `PackageReference` to `coverlet.collector` should be removed from the main project.
- The `<Folder Include="runtimes\win-x64\native\" />` placeholder is just a folder, not a content include.

### 12.3 Strong Naming

```xml
<SignAssembly>true</SignAssembly>
<AssemblyOriginatorKeyFile>$(MSBuildThisFileDirectory)build\AdamSystems.ZVec.NET.snk</AssemblyOriginatorKeyFile>
```

The `.snk` file is committed to the repository (596 bytes in `build/`). This follows the SQLitePCLRaw model of "identity key, not security secret" — appropriate for open source. ✅

The `InternalsVisibleTo` declaration includes the full public key, which is correct for strong-named test assemblies. ✅

### 12.4 CMake/Native Build

The `src/Native/ZVec.Native/` directory contains CMake build scripts and a git submodule pointing to `alibaba/zvec`. The `steps.md` (6,628 bytes) provides detailed Windows build instructions using Ninja. This is well-documented for contributors.

However, there's no CI/CD for building the native binaries. The project plan describes a 6-RID build matrix (win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64) — this is a significant infrastructure investment that hasn't been started.

---

## 13. Recommendations Summary

### Must Fix Before Alpha (P0)

| # | Issue | Effort | Impact |
|---|-------|--------|--------|
| 1 | Replace `nint _handle` with `SafeZvecHandle` in `ZVecCollection` | 4h | Prevents native handle leaks |
| 2 | ~~Add `AsyncReaderWriterLock` usage to `ZVecCollection`~~ 🗑️ Canceled | — | — |
| 3 | Fix version constants (`ExpectedMajor/Minor/Patch`) | 0.5h | Makes the SDK actually usable |
| 4 | Implement schema/options marshalling in `CreateAndOpen`/`Open` | 8h | Makes collections functional |

### Fix Before 1.0 (P1)

| # | Issue | Effort | Impact |
|---|-------|--------|--------|
| 5 | Fix re-initialization after shutdown in `ZVecFactory` | 2h | Prevents undefined native behavior |
| 6 | Replace `WeakReference` tracking with strong refs or `ConditionalWeakTable` | 2h | Prevents orphaned native handles |
| 7 | Address vector copy in `NativeDocUnmarshaller` (zero-allocation goal) | 8h | Core value proposition |
| 8 | Implement sparse vector serialization/deserialization | 6h | Feature completeness |
| 9 | Add missing scalar type handlers (`uint`, `ulong`, arrays) | 3h | Feature completeness |
| 10 | Validate `ZVecFieldValue` struct layout against native definition | 2h | Prevents silent data corruption |
| 11 | Add missing P/Invoke declarations for full C API coverage | 6h | API completeness |

### Near-Term (P2)

| # | Issue | Effort | Impact |
|---|-------|--------|--------|
| 12 | Implement `ZVecFilterBuilder` | 6h | Core usability |
| 13 | Implement `ZVecCollectionSchemaBuilder` | 4h | Core usability |
| 14 | Implement DI extensions | 4h | ASP.NET Core integration |
| 15 | Use `ArrayPool` for batch insert buffers | 2h | Performance |
| 16 | Replace `GCHandle.Alloc(Pinned)` in Delete/Fetch | 3h | Performance |
| 17 | Unify `SafeZvecDocHandle` base class | 0.5h | Consistency |
| 18 | Add `_disposed` flag to `ZVecFactory` | 1h | Correctness |
| 19 | Check `_factoryShutdownToken` in collection operations | 2h | Graceful shutdown |

### Polish (P3)

| # | Issue | Effort | Impact |
|---|-------|--------|--------|
| 20 | Unify `ZVecStatus` / `ZVecWriteResult` type choice | 1h | Consistency |
| 21 | Use `System.Text.Json` for FTS extra params JSON | 2h | Correctness |
| 22 | Fix `ZVecNativeStrings` usage in `NativeIndexParamBuilder` | 0.5h | Maintainability |
| 23 | Add thread-safety to `NativeLibraryResolver` mock support | 1h | Test reliability |
| 24 | Remove dead code (`ResultBufferPool` unused) | 0.5h | Cleanliness |
| 25 | Move `coverlet.collector` to test project | 0.5h | Package size |
| 26 | Complete XML doc comments | 4h | Discoverability |

---

## Appendix A: Code Metrics

| File | Lines | Role |
|------|-------|------|
| `ZVecCollection.cs` | ~530 | Core CRUD/Query/DDL implementation |
| `ZVecFactory.cs` | ~200 | Factory lifecycle & collection management |
| ~~`AsyncReaderWriterLock.cs`~~ 🗑️ Removed | ~287 | ~~Custom reader-writer lock~~ |
| `NativeMethods.cs` | ~441 | P/Invoke declarations |
| `NativeDocBuilder.cs` | ~165 | Doc serialization |
| `NativeDocUnmarshaller.cs` | ~125 | Doc deserialization |
| `NativeIndexParamBuilder.cs` | ~190 | Index param serialization |
| `ZVecDefaults.cs` | ~226 | Centralized defaults |
| `NativeQueryBuilder.cs` | ~70 | Query serialization |
| `ZVecFtsExtraParams.cs` | ~145 | FTS JSON builder |
| All other files | ~400 | Models, enums, handles, errors |
| **Total C# code** | **~2,779** | |

## Appendix B: Implementation Completeness vs Project Plan

| Epic | Description | Plan Tasks | Implemented | % |
|------|-------------|------------|-------------|---|
| E10 | Native C-API Bridge | 4 | 4 | 100% |
| E11 | P/Invoke Layer | 7 | 5 | 71% |
| E12 | CRUD (Collection) | 12 | 8 | 67% |
| E13 | Query | 8 | 2 | 25% |
| E14 | DDL | 6 | 4 | 67% |
| E15 | DI Extensions | 4 | 0 | 0% |
| E16 | Builders (Schema/Filter) | 5 | 0 | 0% |
| E17 | Unit Tests | 8 | 0 | 0% |
| E18 | Integration Tests | 6 | 0 | 0% |
| E19 | Benchmarks | 4 | 0 | 0% |
| E20 | CI/CD | 7 | 0 | 0% |
| **Total** | | **71** | **23** | **32%** |

---

*End of Core Review*
