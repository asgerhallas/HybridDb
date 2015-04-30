@echo off
%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe build.proj /t:%1 /p:Version=%2