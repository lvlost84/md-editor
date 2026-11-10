@echo off
rem MdEditor (WPF) build. Roslyn csc from Visual Studio 2017 (C# 7.3) + the WPF assemblies shipped with Windows. No SDK / NuGet / MSBuild / XAML.
rem ASCII only on purpose (cmd misparses UTF-8). OUTDIR env overrides the output folder.
rem lib\*.dll (AvalonEdit, Markdig, deps) are referenced AND embedded as resources, so the output stays ONE exe (Program.cs resolves them at run time).
cd /d "%~dp0"
set "VSW=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
for /f "usebackq delims=" %%i in (`"%VSW%" -latest -products * -property installationPath`) do set "VS=%%i"
set "CSC=%VS%\MSBuild\15.0\Bin\Roslyn\csc.exe"
set "FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%CSC%" goto nocsc
if "%OUTDIR%"=="" set "OUTDIR=out"
if not exist "%OUTDIR%" mkdir "%OUTDIR%"
if not exist app.ico powershell -NoProfile -ExecutionPolicy Bypass -File make_icon.ps1
set "LIBS="
for %%f in (lib\*.dll) do call set "LIBS=%%LIBS%% /r:lib\%%~nxf /resource:lib\%%~nxf,%%~nxf"
"%CSC%" /nologo /codepage:65001 /langversion:7.3 /target:winexe /platform:anycpu /optimize+ /win32icon:app.ico /resource:app.ico,MdEditor.app.ico /out:"%OUTDIR%\MdEditor.exe" /r:"%FW%\WPF\PresentationFramework.dll" /r:"%FW%\WPF\PresentationCore.dll" /r:"%FW%\WPF\WindowsBase.dll" /r:"%FW%\System.Xaml.dll" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /r:System.Xml.Linq.dll /r:System.Xml.dll /r:System.Web.Extensions.dll /r:System.dll %LIBS% *.cs || exit /b 1
echo.
echo Build OK: %OUTDIR%\MdEditor.exe
goto :eof
:nocsc
echo [ERROR] Roslyn csc.exe not found under Visual Studio 2017
exit /b 1
