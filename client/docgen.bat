@echo off

SET DOXY="C:\Program Files\doxygen\bin\doxygen.exe"

if exist %DOXY% (
   pushd
   cd /d %1
   call %DOXY% Doxyfile
   popd
)

exit 0