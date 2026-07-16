@echo off
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
set CMAKE_POLICY_VERSION_MINIMUM=3.5
set "PATH=%PATH%;%ProgramFiles%\Git\usr\bin;%USERPROFILE%\scoop\shims;%USERPROFILE%\scoop\apps\mingw\current\bin"
where cl
where gcc
where perl
where ninja
cmake -S "%~dp0." -B "%~dp0build" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_POLICY_VERSION_MINIMUM=3.5 -DCMAKE_C_COMPILER=cl -DCMAKE_CXX_COMPILER=cl -DSNOWBALL_HOST_CC=gcc.exe
exit /b %ERRORLEVEL%
