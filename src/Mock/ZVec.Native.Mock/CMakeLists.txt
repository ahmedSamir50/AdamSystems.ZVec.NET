cmake_minimum_required(VERSION 3.12)
project(ZVecNativeMock CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Define the shared library target
add_library(zvec_c_api SHARED src/zvec_c_api_mock.cpp)

target_include_directories(zvec_c_api PRIVATE src)

# Set output directories
set(OUTPUT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../../Native/ZVec.Native/build/external/zvec/bin")
file(MAKE_DIRECTORY "${OUTPUT_DIR}")

set_target_properties(zvec_c_api PROPERTIES
    LIBRARY_OUTPUT_DIRECTORY "${OUTPUT_DIR}"
    RUNTIME_OUTPUT_DIRECTORY "${OUTPUT_DIR}"
    ARCHIVE_OUTPUT_DIRECTORY "${OUTPUT_DIR}"
    POSITION_INDEPENDENT_CODE ON
)
