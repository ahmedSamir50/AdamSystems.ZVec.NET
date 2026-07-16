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
cmake --build "%~dp0build" --config Release --target zvec_c_api --parallel
exit /b %ERRORLEVEL%
