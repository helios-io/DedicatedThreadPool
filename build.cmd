@echo off

pushd %~dp0

tools\nuget\NuGet.exe update -self

tools\nuget\NuGet.exe install FAKE -OutputDirectory src\packages -ExcludeVersion -Version 3.4.1

tools\nuget\NuGet.exe install xunit.runners -OutputDirectory src\packages\FAKE -ExcludeVersion -Version 1.9.2

if not exist src\packages\SourceLink.Fake\tools\SourceLink.fsx ( 
  tools\nuget\nuget.exe install SourceLink.Fake -OutputDirectory src\packages -ExcludeVersion
)
rem cls

set encoding=utf-8
src\packages\FAKE\tools\FAKE.exe build.fsx %*

popd


