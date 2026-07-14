#pragma once
#include <string>
#include <vector>
#include <unordered_map>

struct zvec_field_schema_t {
    std::string name;
    int data_type;
    bool nullable;
    uint32_t dimension;
    void* index_params = nullptr;
};

struct zvec_collection_schema_t {
    std::vector<zvec_field_schema_t*> fields;
};

struct zvec_collection_options_t {
    bool read_only = false;
    bool enable_mmap = true;
};

struct zvec_index_params_t {
    int index_type;
    int metric_type;
    int quantize_type;
    int hnsw_m;
    int hnsw_ef;
    int centroids_num;
    int max_degree;
    int search_list_size;
    float alpha;
    bool saturate_graph;
    bool use_contiguous_memory;
    int diskann_max_degree;
    int diskann_list_size;
    int diskann_pq_chunk;
    bool enable_range;
    bool enable_wildcard;
    std::string tokenizer;
    std::vector<std::string> filters;
    std::string extra_json;
};

struct zvec_doc_t {
    std::string pk;
    float score = 0.0f;
    std::unordered_map<std::string, std::vector<float>> dense_vectors;
    std::unordered_map<std::string, std::vector<int>> sparse_indices;
    std::unordered_map<std::string, std::vector<float>> sparse_values;
    // We mock field value storage using simple strings or integers
    struct Value {
        int type;
        bool b_val;
        int32_t i32_val;
        int64_t i64_val;
        uint32_t ui32_val;
        uint64_t ui64_val;
        float f_val;
        double d_val;
        std::string s_val;
        std::vector<float> vec_val;
    };
    std::unordered_map<std::string, Value> fields;
};

struct zvec_collection_t {
    std::string path;
    zvec_collection_schema_t schema;
    zvec_collection_options_t options;
    std::vector<zvec_doc_t*> docs;
};

struct zvec_vector_query_t {
    std::vector<float> dense_vector;
    std::vector<int> sparse_indices;
    std::vector<float> sparse_values;
    void* query_params = nullptr;
    bool include_vector = true;
};
