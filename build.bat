@echo off
"c:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe" build.proj /t:%1 /p:Version=%2