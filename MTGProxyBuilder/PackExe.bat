@echo off

SET APP_NAME=MTGProxyBuilder.exe
SET ILMERGE_BUILD=Release
SET ILMERGE_VERSION=3.0.29
SET ILMERGE_PATH=%USERPROFILE%\.nuget\packages\ilmerge\%ILMERGE_VERSION%\tools\net452

echo Merging %APP_NAME% ...

"%ILMERGE_PATH%"\ILMerge.exe Bin\%ILMERGE_BUILD%\%APP_NAME%  ^
  /lib:Bin\%ILMERGE_BUILD%\ ^
  /out:%APP_NAME% ^
  PdfSharp.dll ^
  PdfSharp.Charting.dll ^
  Newtonsoft.Json.dll ^
  Microsoft.WindowsAPICodePack.dll ^
  Microsoft.WindowsAPICodePack.Shell.dll

:Done
dir %APP_NAME%