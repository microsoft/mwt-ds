@echo off
set BuildNuget=true
devenv Decision.sln /build
xcopy /y bin\x64\Release\*.nupkg c:\work\LocalNugets

