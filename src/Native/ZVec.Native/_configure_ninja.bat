@echo off
setlocal
call "C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
if errorlevel 1 exit /b 1

set CMAKE_GENERATOR=Ninja
set CMAKE_POLICY_VERSION_MINIMUM=3.5
set "PATH=%PATH%;C:\Program Files\Git\usr\bin;%USERPROFILE%\scoop\shims;%USERPROFILE%\scoop\apps\mingw\current\bin"

set "CMAKE=C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
set "ROOT=D:\A_S\ZVec.Net_SLN\ZVec.Net\src\Native\ZVec.Native"
set "GCC=%USERPROFILE%\scoop\apps\mingw\current\bin\gcc.exe"

cd /d "%ROOT%"
if exist build rmdir /s /q build
mkdir build

"%CMAKE%" -S "%ROOT%" -B "%ROOT%\build" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=cl -DCMAKE_CXX_COMPILER=cl "-DSNOWBALL_HOST_CC=%GCC:\=/%"
if errorlevel 1 exit /b 1

echo CONFIGURE_OK
exit /b 0
