cmake_minimum_required(VERSION 3.26)
project(ZvecNativeBridge CXX C)

# Require C++17 (matches Alibaba ZVec)
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Consume upstream Alibaba zvec; build its official fat C API DLL (zvec_c_api).
set(BUILD_TESTING OFF CACHE BOOL "Disable Zvec tests" FORCE)
set(BUILD_TOOLS OFF CACHE BOOL "Disable Zvec tools" FORCE)
set(BUILD_EXAMPLES OFF CACHE BOOL "Disable Zvec examples" FORCE)
set(BUILD_PYTHON_BINDINGS OFF CACHE BOOL "Disable Zvec Python bindings" FORCE)
set(BUILD_C_BINDINGS ON CACHE BOOL "Build upstream official zvec_c_api" FORCE)

# Snowball codegen uses GNU make + a Unix-style host CC (MinGW gcc on Windows).
if(WIN32 AND NOT SNOWBALL_HOST_CC)
  find_program(_ZVEC_MINGW_GCC NAMES gcc)
  if(_ZVEC_MINGW_GCC)
    set(SNOWBALL_HOST_CC "${_ZVEC_MINGW_GCC}" CACHE STRING "Host C compiler for Snowball codegen" FORCE)
    message(STATUS "SNOWBALL_HOST_CC=${SNOWBALL_HOST_CC}")
  endif()
endif()

add_subdirectory(external/zvec)
