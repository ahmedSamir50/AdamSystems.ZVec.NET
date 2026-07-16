@echo off
setlocal

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if exist "%VSWHERE%" (
    for /f "usebackq tokens=*" %%i in ("%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath) do (
        set "VS_DIR=%%i"
    )
)
if defined VS_DIR (
    call "%VS_DIR%\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
) else (
    echo WARNING: Visual Studio not found automatically.
)
if errorlevel 1 exit /b 1

set CMAKE_GENERATOR=Ninja
set CMAKE_POLICY_VERSION_MINIMUM=3.5
set "PATH=%PATH%;%ProgramFiles%\Git\usr\bin;%USERPROFILE%\scoop\shims;%USERPROFILE%\scoop\apps\mingw\current\bin"

set "CMAKE=cmake"
set "ROOT=%~dp0."
set "RUNTIMES=%~dp0..\..\Core\ZVec.NET\runtimes\win-x64\native"

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
