# ZVec.Native â€” Windows build guide

This folder is a thin CMake wrapper around the Alibaba `external/zvec` submodule. It builds the **upstream official** fat C API shared library:

- CMake target: `zvec_c_api`
- Header of record: `external/zvec/src/include/zvec/c_api.h`
- Artifact: `zvec_c_api.dll` (under `build\`, path may be nested)

There is no separate stub bridge DLL. C# / NuGet P/Invoke loads `zvec_c_api` and maps `zvec_error_code_t` in managed code.

---

## Prerequisites

| Tool | Notes |
|------|--------|
| Visual Studio 2026 | Desktop C++ workload; use **Developer PowerShell for VS 2026** (or `VsDevCmd.bat`) so `cl.exe` is on PATH |
| CMake â‰¥ 3.26 | Bundled with VS is fine |
| Git | Submodule + Snowball scripts; also provides `perl` / `env` under `Git\usr\bin` |
| Scoop: `ninja`, `make`, `mingw`, `perl` | Required on Windows for Ninja builds and Snowball host codegen |

```powershell
scoop install ninja make mingw perl
```

Repo root (this machine): `<workspace-root>`  
Native root: `src\Native\ZVec.Native`

Initialize the submodule once:

```powershell
cd <workspace-root>
git submodule update --init --recursive
```

### Long paths (optional, not sufficient alone)

```bat
reg add "HKLM\SYSTEM\CurrentControlSet\Control\FileSystem" /v LongPathsEnabled /t REG_DWORD /d 1 /f
```

Even with long paths enabled, MSBuild **FileTracker** can still fail at ~260 chars (`FTK1011`) on deep Arrow trees. That is why this guide **requires Ninja** (see below), not only the registry flag.

### Antivirus noise

AV may quarantine unrelated Scoop files under MinGW `opt\` (e.g. `tdbcmysql112.dll`). Ignore those â€” the build does not use them. Escalate only if AV blocks `gcc.exe`, `make.exe`, `ninja.exe`, or MinGW `bin\` runtimes that `gcc` needs.

---

## Recommended: helper scripts (in-tree `build\`)

From **Developer PowerShell for VS 2026** (or any shell after `VsDevCmd`):

```powershell
cd <workspace-root>\src\Native\ZVec.Native
.\_configure_ninja.bat
.\_build_ninja.bat
```

These scripts:

1. Call VsDevCmd (MSVC first on PATH)
2. Append Git `usr\bin`, Scoop shims, MinGW `bin`
3. Set `CMAKE_GENERATOR=Ninja` and `CMAKE_POLICY_VERSION_MINIMUM=3.5`
4. Configure/build **in-tree** `build\` with `-G Ninja` and `SNOWBALL_HOST_CC` â†’ MinGW `gcc`

First configure + build is long (Arrow, RocksDB, protobuf, etc.). Arrow alone can take many minutes after step ~948.

---

## Manual commands (same outcome)

```bat
cd <workspace-root>\src\Native\ZVec.Native
rmdir /s /q build
mkdir build
set CMAKE_GENERATOR=Ninja
set CMAKE_POLICY_VERSION_MINIMUM=3.5
set PATH=%PATH%;%ProgramFiles%\Git\usr\bin;%USERPROFILE%\scoop\shims;%USERPROFILE%\scoop\apps\mingw\current\bin

cmake -S . -B build -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=cl -DCMAKE_CXX_COMPILER=cl -DSNOWBALL_HOST_CC=gcc.exe

cmake --build build --target zvec_c_api --parallel
```

### Success checks

1. Arrow configure log must say `Generator: Ninja` (not Visual Studio):

   `build\external\zvec\thirdparty\arrow\arrow\src\ARROW.BUILD-stamp\ARROW.BUILD-configure-out.log`

2. Find the DLL:

```powershell
Get-ChildItem -Path .\build -Filter zvec_c_api.dll -Recurse
```

### Incremental rebuild

After a successful configure, re-run only:

```bat
cmake --build build --target zvec_c_api --parallel
```

Wipe `build\` and reconfigure when CMake options, generator, or the Arrow MSVC patch change.

---

## Windows Arrow build: MSVC / Ninja workaround (FTK1011)

### Symptom

Building Arrow inside zvec with the default Visual Studio / MSBuild generator can fail on deep paths:

- `FTK1011` / FileTracker `MAX_PATH`
- Nested Arrow deps (utf8proc, etc.)

### What we do (local only)

In the **zvec submodule** file `external/zvec/thirdparty/arrow/CMakeLists.txt`, only the **`elseif (MSVC)`** `ExternalProject_Add(ARROW.BUILD ...)` block is tweaked:

1. Force Ninja: `CMAKE_GENERATOR=Ninja` and `-G Ninja`
2. Single-config Release: `-DCMAKE_BUILD_TYPE=Release`
3. Drop multi-config `--config $<CONFIG>` from BUILD / INSTALL

This does not change Apache Arrow sources and does not affect Android / iOS / Linux / macOS branches in that file.

### After upgrading Alibaba zvec

This tweak lives inside the submodule, so a clean upgrade removes it.

1. Update the submodule (see below).
2. Try a Windows Release build.
3. If Arrow builds fine â†’ nothing to do.
4. If `FTK1011` returns â†’ re-apply the same MSVC/Ninja change in `external/zvec/thirdparty/arrow/CMakeLists.txt`.

Keep the change local / uncommitted in the submodule unless you later fork/patch differently.

### Reference MSVC CONFIGURE/BUILD/INSTALL shape

```cmake
# Force Ninja for Arrow + nested deps (utf8proc, etc.). Default VS generator
# hits MSBuild FileTracker MAX_PATH (FTK1011) under deep Windows trees.
ExternalProject_Add(
        ARROW.BUILD PREFIX arrow
        SOURCE_DIR ${CMAKE_CURRENT_SOURCE_DIR}/apache-arrow-21.0.0
        DOWNLOAD_COMMAND ""
        BUILD_IN_SOURCE false
        CONFIGURE_COMMAND "${CMAKE_COMMAND}" -E env "CMAKE_GENERATOR=Ninja" ${CONFIGURE_ENV_LIST} "${CMAKE_COMMAND}" -G Ninja ${CMAKE_CACHE_ARGS} ${ARROW_EXTRA_CMAKE_ARGS} ${_ARROW_COMPILER_LAUNCHER_ARGS} ${_ARROW_COMPILER_ARGS} -DCMAKE_BUILD_TYPE=Release -DCMAKE_DEBUG_POSTFIX= -DARROW_BUILD_SHARED=OFF -DARROW_ACERO=ON -DARROW_FILESYSTEM=ON -DARROW_DATASET=ON -DARROW_PARQUET=ON -DARROW_COMPUTE=ON -DARROW_WITH_ZLIB=OFF -DARROW_DEPENDENCY_SOURCE=BUNDLED -DARROW_MIMALLOC=OFF -DCMAKE_INSTALL_LIBDIR=lib "<SOURCE_DIR>/cpp"
        BUILD_COMMAND "${CMAKE_COMMAND}" --build . -j ${NPROC}
        INSTALL_COMMAND "${CMAKE_COMMAND}" --install "<BINARY_DIR>" --prefix=${EXTERNAL_BINARY_DIR}/usr/local
        ...
)
```

Upstream originally used `$<CONFIG>` and no `-G Ninja` on those MSVC lines.

---

## Reset / refresh the Alibaba zvec submodule

From repo root `<workspace-root>` in Developer PowerShell:

```powershell
git submodule deinit -f src/Native/ZVec.Native/external/zvec
Remove-Item -Recurse -Force src/Native/ZVec.Native/external/zvec
Remove-Item -Recurse -Force .git/modules/src/Native/ZVec.Native/external/zvec -ErrorAction SilentlyContinue
git submodule update --init --recursive
```

Then re-apply the Arrow MSVC/Ninja tweak if still required, and run `_configure_ninja.bat` / `_build_ninja.bat` again.

---

## What not to edit day-to-day

- Do not hand-edit vendored sources under `external/zvec\` except the documented Arrow MSVC ExternalProject workaround.
- Prefer changing our wrapper [`CMakeLists.txt`](CMakeLists.txt) or bumping the submodule commit.
