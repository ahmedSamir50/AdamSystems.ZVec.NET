@echo off
call "C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
if errorlevel 1 exit /b 1
set CMAKE_POLICY_VERSION_MINIMUM=3.5
set "PATH=%PATH%;C:\Program Files\Git\usr\bin;%USERPROFILE%\scoop\shims;%USERPROFILE%\scoop\apps\mingw\current\bin"
where cl
where gcc
where perl
where ninja
"C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" -S "D:\A_S\ZVec.Net_SLN\ZVec.Net\src\Native\ZVec.Native" -B "D:\A_S\ZVec.Net_SLN\ZVec.Net\src\Native\ZVec.Native\build" -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_POLICY_VERSION_MINIMUM=3.5 -DCMAKE_C_COMPILER=cl -DCMAKE_CXX_COMPILER=cl -DSNOWBALL_HOST_CC=C:/Users/NTG/scoop/apps/mingw/current/bin/gcc.exe
exit /b %ERRORLEVEL%
