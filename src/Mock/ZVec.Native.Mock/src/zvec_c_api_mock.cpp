#include <iostream>
#include <cstring>
#include "mock_structs.h"

#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#define strdup _strdup
#else
#define EXPORT __attribute__((visibility("default")))
#endif

extern "C" {

EXPORT bool zvec_check_version(int major, int minor, int patch) {
    return (major == 0 && minor == 5);
}

EXPORT void zvec_version(int* major, int* minor, int* patch) {
    if (major) *major = 0;
    if (minor) *minor = 5;
    if (patch) *patch = 1;
}

EXPORT int zvec_initialize(void* config) {
    return 0; // OK
}

EXPORT void zvec_shutdown() {
}

EXPORT void zvec_free(void* ptr) {
    free(ptr);
}

EXPORT void* zvec_config_data_create() {
    return malloc(1); // dummy
}

EXPORT void zvec_config_data_set_query_thread_count(void* cfg, uint32_t threads) {}
EXPORT void zvec_config_data_set_memory_limit(void* cfg, uint64_t limit) {}
EXPORT void zvec_config_data_set_log_config(void* cfg, void* log) {}

EXPORT void* zvec_config_log_create_file(int level, const char* dir, const char* basename, uint32_t size, uint32_t overdue) {
    return malloc(1); // dummy
}

EXPORT void* zvec_config_log_create_console(int level) {
    return malloc(1); // dummy
}

// Collections
EXPORT void* zvec_collection_schema_create(const char* name) {
    return new zvec_collection_schema_t();
}

EXPORT void zvec_collection_schema_destroy(void* schema) {
    if (schema) {
        auto* s = static_cast<zvec_collection_schema_t*>(schema);
        for (auto* f : s->fields) {
            delete f;
        }
        delete s;
    }
}

EXPORT int zvec_collection_schema_add_field(void* schema, void* field) {
    if (!schema || !field) return -1;
    auto* s = static_cast<zvec_collection_schema_t*>(schema);
    auto* f = static_cast<zvec_field_schema_t*>(field);
    s->fields.push_back(f);
    return 0;
}

EXPORT void* zvec_collection_options_create() {
    return new zvec_collection_options_t();
}

EXPORT int zvec_collection_options_set_read_only(void* options, bool ro) {
    if (!options) return -1;
    static_cast<zvec_collection_options_t*>(options)->read_only = ro;
    return 0;
}

EXPORT int zvec_collection_options_set_enable_mmap(void* options, bool mmap) {
    if (!options) return -1;
    static_cast<zvec_collection_options_t*>(options)->enable_mmap = mmap;
    return 0;
}

EXPORT int zvec_collection_create_and_open(const char* path, void* schema, void* options, void** handle) {
    if (!path || !handle) return -1;
    auto* col = new zvec_collection_t();
    col->path = path;
    if (schema) {
        // copy fields
        auto* s = static_cast<zvec_collection_schema_t*>(schema);
        for (auto* f : s->fields) {
            auto* nf = new zvec_field_schema_t(*f);
            col->schema.fields.push_back(nf);
        }
    }
    if (options) {
        col->options = *static_cast<zvec_collection_options_t*>(options);
    }
    *handle = col;
    return 0;
}

EXPORT int zvec_collection_open(const char* path, void* options, void** handle) {
    return zvec_collection_create_and_open(path, nullptr, options, handle);
}

EXPORT int zvec_collection_close(void* handle) {
    if (!handle) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    for (auto* doc : col->docs) {
        delete doc;
    }
    for (auto* f : col->schema.fields) {
        delete f;
    }
    delete col;
    return 0;
}

EXPORT int zvec_collection_destroy(void* handle) {
    return 0; 
}

EXPORT int zvec_collection_insert(void* handle, void** docs, size_t count, size_t* success, size_t* error) {
    if (!handle || !docs) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    size_t succ = 0;
    for (size_t i = 0; i < count; i++) {
        if (docs[i]) {
            auto* d = static_cast<zvec_doc_t*>(docs[i]);
            col->docs.push_back(new zvec_doc_t(*d));
            succ++;
        }
    }
    if (success) *success = succ;
    if (error) *error = count - succ;
    return 0;
}

struct zvec_write_result_t {
    int code;
    char* message;
};

EXPORT int zvec_collection_insert_with_results(void* handle, void** docs, size_t count, void*** results, size_t* res_count) {
    if (!handle || !docs || !results || !res_count) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    
    auto** res_arr = (void**)malloc(sizeof(void*) * count);
    for (size_t i = 0; i < count; i++) {
        auto* res = (zvec_write_result_t*)malloc(sizeof(zvec_write_result_t));
        res->code = 0;
        res->message = nullptr;
        if (docs[i]) {
            auto* d = static_cast<zvec_doc_t*>(docs[i]);
            col->docs.push_back(new zvec_doc_t(*d));
        } else {
            res->code = 1;
            res->message = strdup("Null doc pointer");
        }
        res_arr[i] = res;
    }
    *results = res_arr;
    *res_count = count;
    return 0;
}

EXPORT void zvec_write_results_free(void** results, size_t count) {
    if (results) {
        for (size_t i = 0; i < count; i++) {
            if (results[i]) {
                auto* res = static_cast<zvec_write_result_t*>(results[i]);
                if (res->message) free(res->message);
                free(res);
            }
        }
        free(results);
    }
}

EXPORT int zvec_collection_update(void* handle, void** docs, size_t count, size_t* success, size_t* error) {
    return zvec_collection_insert(handle, docs, count, success, error);
}

EXPORT int zvec_collection_upsert(void* handle, void** docs, size_t count, size_t* success, size_t* error) {
    return zvec_collection_insert(handle, docs, count, success, error);
}

EXPORT int zvec_collection_delete(void* handle, void** pks, size_t count, size_t* success, size_t* error) {
    if (!handle || !pks) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    size_t deleted = 0;
    for (size_t i = 0; i < count; i++) {
        const char* pk = (const char*)pks[i];
        for (auto it = col->docs.begin(); it != col->docs.end(); ) {
            if ((*it)->pk == pk) {
                delete *it;
                it = col->docs.erase(it);
                deleted++;
            } else {
                ++it;
            }
        }
    }
    if (success) *success = deleted;
    return 0;
}

EXPORT int zvec_collection_delete_with_results(void* handle, void** pks, size_t count, void*** results, size_t* res_count) {
    if (!handle || !pks || !results || !res_count) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    
    auto** res_arr = (void**)malloc(sizeof(void*) * count);
    for (size_t i = 0; i < count; i++) {
        auto* res = (zvec_write_result_t*)malloc(sizeof(zvec_write_result_t));
        res->code = 0;
        res->message = nullptr;
        const char* pk = (const char*)pks[i];
        bool found = false;
        for (auto it = col->docs.begin(); it != col->docs.end(); ) {
            if ((*it)->pk == pk) {
                delete *it;
                it = col->docs.erase(it);
                found = true;
                break;
            } else {
                ++it;
            }
        }
        if (!found) {
            res->code = 1;
            res->message = strdup("Key not found");
        }
        res_arr[i] = res;
    }
    *results = res_arr;
    *res_count = count;
    return 0;
}

EXPORT int zvec_collection_delete_by_filter(void* handle, const char* filter) {
    return 0;
}

EXPORT int zvec_collection_fetch(void* handle, void** pks, size_t count, void* fields, size_t field_count, bool include_vector, void*** docs, size_t* docs_count) {
    if (!handle || !pks || !docs || !docs_count) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    std::vector<zvec_doc_t*> found_docs;
    for (size_t i = 0; i < count; i++) {
        const char* pk = (const char*)pks[i];
        for (auto* d : col->docs) {
            if (d->pk == pk) {
                found_docs.push_back(new zvec_doc_t(*d));
            }
        }
    }
    auto** docs_arr = (void**)malloc(sizeof(void*) * found_docs.size());
    for (size_t i = 0; i < found_docs.size(); i++) {
        docs_arr[i] = found_docs[i];
    }
    *docs = docs_arr;
    *docs_count = found_docs.size();
    return 0;
}

EXPORT void zvec_docs_free(void** docs, size_t count) {
    if (docs) {
        for (size_t i = 0; i < count; i++) {
            if (docs[i]) {
                delete static_cast<zvec_doc_t*>(docs[i]);
            }
        }
        free(docs);
    }
}

// DDL
EXPORT int zvec_collection_add_column(void* handle, void* field, const char* def_expr) {
    if (!handle || !field) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    auto* f = static_cast<zvec_field_schema_t*>(field);
    col->schema.fields.push_back(new zvec_field_schema_t(*f));
    return 0;
}

EXPORT int zvec_collection_drop_column(void* handle, const char* columnName) {
    if (!handle || !columnName) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    for (auto it = col->schema.fields.begin(); it != col->schema.fields.end(); ) {
        if ((*it)->name == columnName) {
            delete *it;
            it = col->schema.fields.erase(it);
        } else {
            ++it;
        }
    }
    return 0;
}

EXPORT int zvec_collection_alter_column(void* handle, const char* columnName, const char* newName, void* newSchema) {
    if (!handle || !columnName) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    for (auto* f : col->schema.fields) {
        if (f->name == columnName) {
            if (newName) f->name = newName;
            if (newSchema) {
                auto* ns = static_cast<zvec_field_schema_t*>(newSchema);
                f->data_type = ns->data_type;
                f->nullable = ns->nullable;
                f->dimension = ns->dimension;
            }
        }
    }
    return 0;
}

EXPORT int zvec_collection_alter_column_rename(void* handle, const char* columnName, const char* newName) {
    return zvec_collection_alter_column(handle, columnName, newName, nullptr);
}

EXPORT int zvec_collection_create_index(void* handle, const char* columnName, void* indexParam) {
    return 0;
}

EXPORT int zvec_collection_drop_index(void* handle, const char* columnName) {
    return 0;
}

EXPORT int zvec_collection_optimize(void* handle) {
    return 0;
}

// Fields Schemas
EXPORT void* zvec_field_schema_create(const char* name, int dataType, bool nullable, uint32_t dimension) {
    auto* f = new zvec_field_schema_t();
    f->name = name ? name : "";
    f->data_type = dataType;
    f->nullable = nullable;
    f->dimension = dimension;
    return f;
}

EXPORT void zvec_field_schema_destroy(void* field) {
    if (field) delete static_cast<zvec_field_schema_t*>(field);
}

EXPORT int zvec_field_schema_set_index_params(void* field, void* params) {
    if (!field) return -1;
    static_cast<zvec_field_schema_t*>(field)->index_params = params;
    return 0;
}

// Index Params
EXPORT void* zvec_index_params_create(int indexType) {
    auto* p = new zvec_index_params_t();
    p->index_type = indexType;
    return p;
}

EXPORT void zvec_index_params_destroy(void* params) {
    if (params) delete static_cast<zvec_index_params_t*>(params);
}

EXPORT int zvec_index_params_set_metric_type(void* params, int metricType) {
    if (!params) return -1;
    static_cast<zvec_index_params_t*>(params)->metric_type = metricType;
    return 0;
}

EXPORT int zvec_index_params_set_quantize_type(void* params, int quantizeType) {
    if (!params) return -1;
    static_cast<zvec_index_params_t*>(params)->quantize_type = quantizeType;
    return 0;
}

EXPORT int zvec_index_params_set_hnsw_params(void* params, int m, int ef) {
    if (!params) return -1;
    auto* p = static_cast<zvec_index_params_t*>(params);
    p->hnsw_m = m;
    p->hnsw_ef = ef;
    return 0;
}

EXPORT int zvec_index_params_set_ivf_params(void* params, int centroids, int nlist, bool double_level) {
    if (!params) return -1;
    static_cast<zvec_index_params_t*>(params)->centroids_num = centroids;
    return 0;
}

EXPORT int zvec_index_params_set_vamana_params(void* params, int maxDegree, int searchList, float alpha, bool saturate, bool contiguous) {
    if (!params) return -1;
    auto* p = static_cast<zvec_index_params_t*>(params);
    p->max_degree = maxDegree;
    p->search_list_size = searchList;
    p->alpha = alpha;
    p->saturate_graph = saturate;
    p->use_contiguous_memory = contiguous;
    return 0;
}

EXPORT int zvec_index_params_set_diskann_params(void* params, int maxDegree, int listSize, int pqChunk) {
    if (!params) return -1;
    auto* p = static_cast<zvec_index_params_t*>(params);
    p->diskann_max_degree = maxDegree;
    p->diskann_list_size = listSize;
    p->diskann_pq_chunk = pqChunk;
    return 0;
}

EXPORT int zvec_index_params_set_invert_params(void* params, bool range, bool wildcard) {
    if (!params) return -1;
    auto* p = static_cast<zvec_index_params_t*>(params);
    p->enable_range = range;
    p->enable_wildcard = wildcard;
    return 0;
}

EXPORT int zvec_index_params_set_fts_params(void* params, const char* tokenizer, void* filters, const char* extra) {
    if (!params) return -1;
    auto* p = static_cast<zvec_index_params_t*>(params);
    p->tokenizer = tokenizer ? tokenizer : "";
    p->extra_json = extra ? extra : "";
    return 0;
}

// String Array (dummy implementation)
EXPORT void* zvec_string_array_create(size_t cap) {
    return malloc(1);
}
EXPORT void zvec_string_array_add(void* arr, size_t idx, const char* str) {}
EXPORT void zvec_string_array_destroy(void* arr) {
    if (arr) free(arr);
}

// Documents
EXPORT void* zvec_doc_create() {
    return new zvec_doc_t();
}

EXPORT void zvec_doc_destroy(void* doc) {
    if (doc) delete static_cast<zvec_doc_t*>(doc);
}

EXPORT void zvec_doc_set_pk(void* doc, const char* pk) {
    if (doc && pk) static_cast<zvec_doc_t*>(doc)->pk = pk;
}

EXPORT void zvec_doc_set_score(void* doc, float score) {
    if (doc) static_cast<zvec_doc_t*>(doc)->score = score;
}

struct zvec_string_t {
    char* str;
    size_t len;
};

struct zvec_float_array_t {
    float* data;
    size_t len;
};

struct zvec_byte_array_t {
    uint8_t* data;
    size_t len;
};

union zvec_field_value_union {
    bool b_val;
    int32_t i32_val;
    int64_t i64_val;
    uint32_t ui32_val;
    uint64_t ui64_val;
    float f_val;
    double d_val;
    zvec_string_t s_val;
    zvec_float_array_t vec_val;
    zvec_byte_array_t bin_val;
};

EXPORT int zvec_doc_add_field_by_value(void* doc, const char* fieldName, int dataType, const void* value, size_t value_size) {
    if (!doc || !fieldName || !value) return -1;
    auto* d = static_cast<zvec_doc_t*>(doc);
    
    zvec_doc_t::Value mv;
    mv.type = dataType;
    
    switch (dataType) {
        case 3: mv.b_val = *(const bool*)value; break;
        case 4: mv.i32_val = *(const int32_t*)value; break;
        case 5: mv.i64_val = *(const int64_t*)value; break;
        case 6: mv.ui32_val = *(const uint32_t*)value; break;
        case 7: mv.ui64_val = *(const uint64_t*)value; break;
        case 8: mv.f_val = *(const float*)value; break;
        case 9: mv.d_val = *(const double*)value; break;
        case 2:
            // String: value is a UTF-8 char*, value_size is byte length
            mv.s_val = std::string((const char*)value, value_size);
            break;
        case 23:
            // VectorFp32: value is float*, value_size is byte count
            if (value_size % sizeof(float) == 0) {
                size_t float_count = value_size / sizeof(float);
                const float* fptr = (const float*)value;
                mv.vec_val = std::vector<float>(fptr, fptr + float_count);
            }
            break;
    }
    d->fields[fieldName] = mv;
    return 0;
}

EXPORT int zvec_doc_add_sparse_vector_field(void* doc, const char* fieldName, void* indices, void* values, size_t count) {
    if (!doc || !fieldName || !indices || !values) return -1;
    auto* d = static_cast<zvec_doc_t*>(doc);
    int* idx = static_cast<int*>(indices);
    float* val = static_cast<float*>(values);
    d->sparse_indices[fieldName] = std::vector<int>(idx, idx + count);
    d->sparse_values[fieldName] = std::vector<float>(val, val + count);
    return 0;
}

EXPORT char* zvec_doc_get_pk_copy(void* doc) {
    if (!doc) return nullptr;
    return strdup(static_cast<zvec_doc_t*>(doc)->pk.c_str());
}

EXPORT float zvec_doc_get_score(void* doc) {
    if (!doc) return 0.0f;
    return static_cast<zvec_doc_t*>(doc)->score;
}

EXPORT int zvec_doc_get_field_names(void* doc, char*** names, size_t* count) {
    if (!doc || !names || !count) return -1;
    auto* d = static_cast<zvec_doc_t*>(doc);
    *count = d->fields.size();
    if (*count == 0) {
        *names = nullptr;
        return 0;
    }
    char** arr = (char**)malloc(sizeof(char*) * (*count));
    size_t i = 0;
    for (auto const& [key, val] : d->fields) {
        arr[i++] = strdup(key.c_str());
    }
    *names = arr;
    return 0;
}

EXPORT int zvec_doc_get_field_value_pointer(void* doc, const char* fieldName, int dataType, void** value, size_t* value_size) {
    if (!doc || !fieldName || !value || !value_size) return -1;
    auto* d = static_cast<zvec_doc_t*>(doc);
    auto it = d->fields.find(fieldName);
    if (it == d->fields.end()) return -1;
    
    auto& mv = it->second;
    *value_size = 0;

    switch (dataType) {
        case 3: { static bool bv; bv = mv.b_val; *value = &bv; *value_size = sizeof(bool); break; }
        case 4: { static int32_t iv; iv = mv.i32_val; *value = &iv; *value_size = sizeof(int32_t); break; }
        case 5: { static int64_t lv; lv = mv.i64_val; *value = &lv; *value_size = sizeof(int64_t); break; }
        case 6: { static uint32_t uiv; uiv = mv.ui32_val; *value = &uiv; *value_size = sizeof(uint32_t); break; }
        case 7: { static uint64_t ulv; ulv = mv.ui64_val; *value = &ulv; *value_size = sizeof(uint64_t); break; }
        case 8: { static float fv; fv = mv.f_val; *value = &fv; *value_size = sizeof(float); break; }
        case 9: { static double dv; dv = mv.d_val; *value = &dv; *value_size = sizeof(double); break; }
        case 2:
            // Return pointer into the string data
            *value = (void*)mv.s_val.data();
            *value_size = mv.s_val.size();
            break;
        case 23:
            // Return pointer to vector data
            *value = (void*)mv.vec_val.data();
            *value_size = mv.vec_val.size() * sizeof(float);
            break;
        default:
            return -1;
    }
    return 0;
}

EXPORT int zvec_doc_get_sparse_vector_field(void* doc, const char* fieldName, void** indices, void** values, size_t* count) {
    if (!doc || !fieldName || !indices || !values || !count) return -1;
    auto* d = static_cast<zvec_doc_t*>(doc);
    auto it_idx = d->sparse_indices.find(fieldName);
    auto it_val = d->sparse_values.find(fieldName);
    if (it_idx == d->sparse_indices.end() || it_val == d->sparse_values.end()) return -1;
    
    *count = it_idx->second.size();
    
    int* idx_arr = (int*)malloc(sizeof(int) * (*count));
    memcpy(idx_arr, it_idx->second.data(), sizeof(int) * (*count));
    *indices = idx_arr;
    
    float* val_arr = (float*)malloc(sizeof(float) * (*count));
    memcpy(val_arr, it_val->second.data(), sizeof(float) * (*count));
    *values = val_arr;
    
    return 0;
}

// Queries
EXPORT void* zvec_vector_query_create() {
    return new zvec_vector_query_t();
}

EXPORT void zvec_vector_query_destroy(void* query) {
    if (query) delete static_cast<zvec_vector_query_t*>(query);
}

EXPORT int zvec_vector_query_set_vector(void* query, float* data, size_t len) {
    if (!query) return -1;
    auto* q = static_cast<zvec_vector_query_t*>(query);
    q->dense_vector = std::vector<float>(data, data + len);
    return 0;
}

EXPORT int zvec_vector_query_set_sparse_vector(void* query, void* indices, void* values, size_t count) {
    if (!query) return -1;
    auto* q = static_cast<zvec_vector_query_t*>(query);
    int* idx = static_cast<int*>(indices);
    float* val = static_cast<float*>(values);
    q->sparse_indices = std::vector<int>(idx, idx + count);
    q->sparse_values = std::vector<float>(val, val + count);
    return 0;
}

EXPORT int zvec_vector_query_set_query_params(void* query, void* params) {
    if (!query) return -1;
    static_cast<zvec_vector_query_t*>(query)->query_params = params;
    return 0;
}

EXPORT int zvec_vector_query_set_include_vector(void* query, bool include) {
    if (!query) return -1;
    static_cast<zvec_vector_query_t*>(query)->include_vector = include;
    return 0;
}

EXPORT int zvec_collection_query(void* handle, void* query, bool include_vector, void*** docs, size_t* count) {
    if (!handle || !docs || !count) return -1;
    auto* col = static_cast<zvec_collection_t*>(handle);
    std::vector<zvec_doc_t*> results;
    for (auto* d : col->docs) {
        results.push_back(new zvec_doc_t(*d));
    }
    
    auto** docs_arr = (void**)malloc(sizeof(void*) * results.size());
    for (size_t i = 0; i < results.size(); i++) {
        docs_arr[i] = results[i];
    }
    *docs = docs_arr;
    *count = results.size();
    return 0;
}

}
