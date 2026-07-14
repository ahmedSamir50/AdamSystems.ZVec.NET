using System;
using System.Runtime.InteropServices;

namespace AdamSystems.ZVec.NET.Interop;

/// <summary>
/// Provides P/Invoke declarations for the zvec_c_api library.
/// Ensure all signatures map precisely to the corresponding C API declarations in c_api.h.
/// </summary>
internal static partial class NativeMethods
{
    private const string LibraryName = "zvec_c_api";

    // Expected native SemVer pinned at build time (also embedded in NuGet +zvec metadata)
    internal const int ExpectedMajor = 1;
    internal const int ExpectedMinor = 0;
    internal const int ExpectedPatch = 0;

    // =========================================================================
    // Version APIs
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_get_version();

    internal static string GetVersionString()
    {
        IntPtr ptr = zvec_get_version();
        return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    [LibraryImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool zvec_check_version(int major, int minor, int patch);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_version_major();

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_version_minor();

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_version_patch();

    // =========================================================================
    // Init & Error Retrieval
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial int zvec_initialize(IntPtr configData);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_shutdown();

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_last_error(out IntPtr errorMsg);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_last_error_details(IntPtr errorDetails);

    // =========================================================================
    // Collection Lifecycle & Options
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_create_and_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        IntPtr schema,
        IntPtr collectionOptions,
        out IntPtr outCollection);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        IntPtr collectionOptions,
        out IntPtr outCollection);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_close(IntPtr collection);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_destroy(IntPtr collection);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_options_set_read_only(
        IntPtr options,
        [MarshalAs(UnmanagedType.U1)] bool readOnly);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_options_set_enable_mmap(
        IntPtr options,
        [MarshalAs(UnmanagedType.U1)] bool enableMmap);

    // =========================================================================
    // CRUD Operations
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_insert(
        IntPtr collection,
        IntPtr docs,
        nuint docCount,
        out nuint successCount,
        out nuint errorCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_update(
        IntPtr collection,
        IntPtr docs,
        nuint docCount,
        out nuint successCount,
        out nuint errorCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_upsert(
        IntPtr collection,
        IntPtr docs,
        nuint docCount,
        out nuint successCount,
        out nuint errorCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_delete(
        IntPtr collection,
        IntPtr primaryKeys,
        nuint keyCount,
        out nuint successCount,
        out nuint errorCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_delete_by_filter(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filter);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_fetch(
        IntPtr collection,
        IntPtr primaryKeys,
        nuint keyCount,
        out IntPtr results,
        out nuint resultCount);

    // =========================================================================
    // Query Operations
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_query(
        IntPtr collection,
        IntPtr query,
        out IntPtr results,
        out nuint resultCount);

    // =========================================================================
    // DDL Operations
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_add_column(
        IntPtr collection,
        IntPtr fieldSchema,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string expression);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_drop_column(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string columnName);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_alter_column(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string oldName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newName,
        IntPtr newSchema);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_create_index(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string columnName,
        IntPtr indexParam);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_drop_index(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string columnName);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_optimize(
        IntPtr collection);
}
