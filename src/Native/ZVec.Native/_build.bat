@echo off
call "C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\Tools\VsDevCmd.bat" -arch=amd64 -host_arch=amd64
if errorlevel 1 exit /b 1
set CMAKE_POLICY_VERSION_MINIMUM=3.5
set "PATH=%PATH%;C:\Program Files\Git\usr\bin;%USERPROFILE%\scoop\shims;%USERPROFILE%\scoop\apps\mingw\current\bin"
"C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe" --build "D:\A_S\ZVec.Net_SLN\ZVec.Net\src\Native\ZVec.Native\build" --config Release --target zvec_c_api --parallel
exit /b %ERRORLEVEL%
