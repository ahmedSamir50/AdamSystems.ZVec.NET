@echo off
setlocal

call "C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
if errorlevel 1 exit /b 1

set CMAKE_GENERATOR=Ninja
set CMAKE_POLICY_VERSION_MINIMUM=3.5
set "PATH=%PATH%;C:\Program Files\Git\usr\bin;%USERPROFILE%\scoop\shims;%USERPROFILE%\scoop\apps\mingw\current\bin"

set "CMAKE=C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
set "ROOT=D:\A_S\ZVec.Net_SLN\ZVec.Net\src\Native\ZVec.Native"
set "RUNTIMES=D:\A_S\ZVec.Net_SLN\ZVec.Net\src\Core\AdamSystems.ZVec.NET\runtimes\win-x64\native"

echo ============================================
echo Step 1: Build zvec_c_api.dll
echo ============================================
"%CMAKE%" --build "%ROOT%\build" --target zvec_c_api --parallel
if errorlevel 1 (
    echo BUILD FAILED. Try running _configure_ninja.bat first.
    exit /b 1
)

echo ============================================
echo Step 2: Verify exports contain zvec_get_version
echo ============================================
where dumpbin >nul 2>nul
if not errorlevel 1 (
    dumpbin /exports "%ROOT%\build\external\zvec\bin\zvec_c_api.dll" | findstr "zvec_get_version" >nul
    if errorlevel 1 (
        echo WARNING: zvec_get_version NOT found in exports!
        echo Run a clean rebuild: delete build\ then _configure_ninja.bat
    ) else (
        echo OK: zvec_get_version is exported
    )
)

echo ============================================
echo Step 3: Deploy to .NET runtimes folder
echo ============================================
if not exist "%RUNTIMES%" mkdir "%RUNTIMES%"
copy /Y "%ROOT%\build\external\zvec\bin\zvec_c_api.dll" "%RUNTIMES%\zvec_c_api.dll"
if errorlevel 1 (
    echo COPY FAILED
    exit /b 1
)

echo ============================================
echo DONE: zvec_c_api.dll deployed to runtimes
echo ============================================

exit /b 0
