@echo off

rem dotnet build RazorEngine.NetCore.sln -c Release -p:DebugType=None -p:Platform="Any CPU"
dotnet pack RazorEngine.NetCore.sln -c Release

@if errorlevel 1 goto error

@goto exit
:error
@pause

:exit
