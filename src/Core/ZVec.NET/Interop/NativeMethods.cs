using System.Runtime.InteropServices;

namespace ZVec.NET.Interop;

/// <summary>
/// Provides P/Invoke declarations for the zvec_c_api library.
/// Ensure all signatures map precisely to the corresponding C API declarations in c_api.h.
/// </summary>
internal static partial class NativeMethods
{
    internal const string LibraryName = "zvec_c_api";



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
    internal static partial IntPtr zvec_config_data_create();

    [LibraryImport(LibraryName)]
    internal static partial void zvec_config_data_destroy(IntPtr config);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_config_data_set_memory_limit(IntPtr config, ulong memoryLimitBytes);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_config_data_set_log_config(IntPtr config, IntPtr logConfig);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_config_data_set_query_thread_count(IntPtr config, uint threadCount);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_config_log_create_console(int level);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_config_log_create_file(
        int level,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string dir,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string basename,
        uint fileSize,
        uint overdueDays);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_config_log_destroy(IntPtr config);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_shutdown();

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_last_error(out IntPtr errorMsg);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_last_error_details(IntPtr errorDetails);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_free(IntPtr ptr);

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
    internal static partial void zvec_collection_schema_destroy(IntPtr schema);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_collection_schema_create(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_schema_add_field(IntPtr schema, IntPtr fieldSchema);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_collection_options_create();

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_stats(IntPtr collection, out IntPtr statsJson);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_options_set_read_only(
        IntPtr options,
        [MarshalAs(UnmanagedType.U1)] bool readOnly);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_options_set_enable_mmap(
        IntPtr options,
        [MarshalAs(UnmanagedType.U1)] bool enableMmap);

    // =========================================================================
    // CRUD Operations (DML)
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_insert(
        IntPtr collection,
        IntPtr docs, // zvec_doc_t**
        nuint docCount,
        out nuint successCount,
        out nuint errorCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_insert_with_results(
        IntPtr collection,
        IntPtr docs, // zvec_doc_t**
        nuint docCount,
        out IntPtr results, // zvec_write_result_t**
        out nuint resultCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_update(
        IntPtr collection,
        IntPtr docs,
        nuint docCount,
        out nuint successCount,
        out nuint errorCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_update_with_results(
        IntPtr collection,
        IntPtr docs,
        nuint docCount,
        out IntPtr results,
        out nuint resultCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_upsert(
        IntPtr collection,
        IntPtr docs,
        nuint docCount,
        out nuint successCount,
        out nuint errorCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_upsert_with_results(
        IntPtr collection,
        IntPtr docs,
        nuint docCount,
        out IntPtr results,
        out nuint resultCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_delete(
        IntPtr collection,
        IntPtr primaryKeys, // char**
        nuint keyCount,
        out nuint successCount,
        out nuint errorCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_delete_with_results(
        IntPtr collection,
        IntPtr primaryKeys,
        nuint keyCount,
        out IntPtr results,
        out nuint resultCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_delete_by_filter(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filter);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_fetch(
        IntPtr collection,
        IntPtr primaryKeys, // char**
        nuint keyCount,
        IntPtr outputFields, // char**
        nuint outputFieldCount,
        [MarshalAs(UnmanagedType.U1)] bool includeVector,
        out IntPtr documents, // zvec_doc_t***
        out nuint foundCount);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_write_results_free(IntPtr results, nuint resultCount);

    // =========================================================================
    // Query Operations (DQL)
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_query(
        IntPtr collection,
        IntPtr query, // zvec_vector_query_t*
        out IntPtr results, // zvec_doc_t***
        out nuint resultCount);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_multi_query(
        IntPtr collection,
        IntPtr multiQuery,
        out IntPtr results,
        out nuint resultCount);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_multi_query_destroy(IntPtr query);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_multi_query_create();

    [LibraryImport(LibraryName)]
    internal static partial int zvec_multi_query_add_sub_query(IntPtr query, IntPtr subQuery);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_multi_query_set_topk(IntPtr query, int topk);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_multi_query_set_filter(IntPtr query, [MarshalAs(UnmanagedType.LPUTF8Str)] string filter);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_multi_query_set_rerank_rrf(IntPtr query, int rankConstant);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_multi_query_set_rerank_weighted(IntPtr query, IntPtr weights, nuint weightCount);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_sub_query_create();

    [LibraryImport(LibraryName)]
    internal static partial void zvec_sub_query_destroy(IntPtr query);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_sub_query_set_field_name(IntPtr query, [MarshalAs(UnmanagedType.LPUTF8Str)] string fieldName);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_sub_query_set_query_vector(IntPtr query, IntPtr data, nuint size);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_sub_query_set_fts(IntPtr query, IntPtr fts);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_sub_query_set_fts_params(IntPtr query, IntPtr paramsPtr);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_docs_free(IntPtr documents, nuint count);

    // =========================================================================
    // DDL Operations
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_add_column(
        IntPtr collection,
        IntPtr fieldSchema,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? expression);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_drop_column(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string columnName);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_alter_column(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string oldName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? newName,
        IntPtr newSchema);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_alter_column_rename(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string oldName,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newName);

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

    // =========================================================================
    // Doc Builder (zvec_doc_t)
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_doc_create();

    [LibraryImport(LibraryName)]
    internal static partial void zvec_doc_destroy(IntPtr doc);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_doc_set_pk(
        IntPtr doc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string pk);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_doc_set_score(IntPtr doc, float score);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_doc_add_field_by_value(
        IntPtr doc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fieldName,
        int dataType, // zvec_data_type_t
        IntPtr value,          // const void* value â€” raw typed payload
        nuint valueSize);      // size_t value_size â€” byte size of value

    [LibraryImport(LibraryName)]
    internal static partial int zvec_doc_add_sparse_vector_field(
        IntPtr doc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fieldName,
        IntPtr indices,
        IntPtr values,
        nuint count);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_doc_get_field_value_copy(
        IntPtr doc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fieldName,
        int dataType,
        out IntPtr value,        // void** â€” allocated by native, must be freed via zvec_free
        out nuint valueSize);    // size_t* â€” byte count of the returned buffer

    [LibraryImport(LibraryName)]
    internal static partial int zvec_doc_get_field_value_pointer(
        IntPtr doc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fieldName,
        int dataType,
        out IntPtr value,        // const void** â€” points into document-owned memory, do NOT free
        out nuint valueSize);    // size_t* â€” byte count of the returned buffer

    [LibraryImport(LibraryName)]
    internal static partial int zvec_doc_get_field_names(
        IntPtr doc,
        out IntPtr fieldNames, // char***
        out nuint count);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_free_str_array(IntPtr array, nuint count);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_doc_get_sparse_vector_field(
        IntPtr doc,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fieldName,
        out IntPtr indices,
        out IntPtr values,
        out nuint count);

    // =========================================================================
    // Query Builder (zvec_vector_query_t)
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_vector_query_create();

    [LibraryImport(LibraryName)]
    internal static partial void zvec_vector_query_destroy(IntPtr query);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_vector_query_set_topk(IntPtr query, int topk);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_vector_query_set_field_name(
        IntPtr query,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string fieldName);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_vector_query_set_query_vector(
        IntPtr query,
        IntPtr data,
        nuint size);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_vector_query_set_filter(
        IntPtr query,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string filter);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_vector_query_set_include_vector(
        IntPtr query,
        [MarshalAs(UnmanagedType.U1)] bool include);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_vector_query_set_sparse_vector(
        IntPtr query,
        IntPtr indices,
        IntPtr values,
        nuint count);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_vector_query_set_query_params(IntPtr query, IntPtr queryParams);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_fts_create();

    [LibraryImport(LibraryName)]
    internal static partial void zvec_fts_destroy(IntPtr fts);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_fts_set_query_string(IntPtr fts, [MarshalAs(UnmanagedType.LPUTF8Str)] string queryString);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_fts_set_match_string(IntPtr fts, [MarshalAs(UnmanagedType.LPUTF8Str)] string matchString);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_vector_query_set_fts(IntPtr query, IntPtr fts);

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_query_params_fts_create([MarshalAs(UnmanagedType.LPUTF8Str)] string defaultOperator);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_query_params_fts_destroy(IntPtr paramsPtr);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_vector_query_set_fts_params(IntPtr query, IntPtr paramsPtr);


    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_doc_get_pk_copy(IntPtr doc);

    [LibraryImport(LibraryName)]
    internal static partial float zvec_doc_get_score(IntPtr doc);

    // =========================================================================
    // String Array (zvec_string_array_t)
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_string_array_create(nuint count);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_string_array_add(
        IntPtr array,
        nuint idx,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string str);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_string_array_destroy(IntPtr array);

    // =========================================================================
    // Index Params (zvec_index_params_t)
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_index_params_create(int indexType);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_index_params_destroy(IntPtr paramsPtr);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_index_params_set_metric_type(IntPtr paramsPtr, int metricType);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_index_params_set_quantize_type(IntPtr paramsPtr, int quantizeType);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_index_params_set_hnsw_params(IntPtr paramsPtr, int m, int efConstruction);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_index_params_set_vamana_params(
        IntPtr paramsPtr, 
        int maxDegree, 
        int searchListSize, 
        float alpha, 
        [MarshalAs(UnmanagedType.U1)] bool saturateGraph, 
        [MarshalAs(UnmanagedType.U1)] bool useContiguousMemory);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_index_params_set_diskann_params(IntPtr paramsPtr, int maxDegree, int listSize, int pqChunkNum);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_index_params_set_ivf_params(IntPtr paramsPtr, int nList, int nIters, [MarshalAs(UnmanagedType.U1)] bool useSoar);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_index_params_set_invert_params(IntPtr paramsPtr, [MarshalAs(UnmanagedType.U1)] bool enableRangeOpt, [MarshalAs(UnmanagedType.U1)] bool enableWildcard);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_index_params_set_fts_params(
        IntPtr paramsPtr, 
        [MarshalAs(UnmanagedType.LPUTF8Str)] string tokenizerName,
        IntPtr filters,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? extraParams);

    // =========================================================================
    // Field Schema (zvec_field_schema_t)
    // =========================================================================

    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_field_schema_create(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
        int dataType,
        [MarshalAs(UnmanagedType.U1)] bool nullable,
        uint dimension);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_field_schema_destroy(IntPtr schemaPtr);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_field_schema_set_index_params(IntPtr schemaPtr, IntPtr indexParamsPtr);

}
