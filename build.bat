@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "VERSION=1.0.0"
set "OUT=%~dp0Balatro Trainer v%VERSION%"
set "NATIVE_BUILD=%~dp0native\build"
set "PUBLISH_TMP=%~dp0gui\publish_tmp"

echo.
echo ==== Balatro Trainer v%VERSION% - build ====
echo.

rem ---------------------------------------------------------------- prerequisites
where cmake >nul 2>&1
if errorlevel 1 ( echo [ERROR] cmake not found in PATH. Install CMake and the Visual Studio C++ Build Tools. & goto :fail )
where git >nul 2>&1
if errorlevel 1 ( echo [ERROR] git not found in PATH. It is needed to download MinHook. & goto :fail )
where dotnet >nul 2>&1
if errorlevel 1 ( echo [ERROR] dotnet not found in PATH. Install the .NET SDK. & goto :fail )

rem ---------------------------------------------------------------- 1) native backend
echo [1/4] Configuring native backend ^(x64^)...
cmake -S native -B "%NATIVE_BUILD%" -A x64
if errorlevel 1 ( echo [ERROR] CMake configure failed. Is Visual Studio Build Tools with the C++ workload installed? & goto :fail )

echo.
echo [2/4] Building BalatroTrainer.dll ^(Release^)...
cmake --build "%NATIVE_BUILD%" --config Release
if errorlevel 1 ( echo [ERROR] Native build failed. & goto :fail )

if not exist "%NATIVE_BUILD%\Release\BalatroTrainer.dll" ( echo [ERROR] BalatroTrainer.dll was not produced. & goto :fail )

rem ---------------------------------------------------------------- 2) GUI
echo.
echo [3/4] Building the WinForms GUI...
if exist "%PUBLISH_TMP%" rmdir /s /q "%PUBLISH_TMP%"
dotnet publish "gui\BalatroTrainer.Gui.csproj" -c Release -o "%PUBLISH_TMP%" --nologo
if errorlevel 1 (
    echo [ERROR] GUI build failed.
    echo         If the error mentions the target framework, open gui\BalatroTrainer.Gui.csproj and
    echo         change net10.0-windows to the version of your installed .NET SDK, e.g. net8.0-windows.
    goto :fail
)

rem ---------------------------------------------------------------- 3) package
echo.
echo [4/4] Creating "%OUT%"...
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%"
if errorlevel 1 ( echo [ERROR] Could not create the output folder. & goto :fail )

xcopy "%PUBLISH_TMP%\*" "%OUT%\" /e /i /y /q >nul
if errorlevel 1 ( echo [ERROR] Copying the GUI files failed. & goto :fail )

copy /y "%NATIVE_BUILD%\Release\BalatroTrainer.dll" "%OUT%\" >nul
if errorlevel 1 ( echo [ERROR] Copying BalatroTrainer.dll failed. & goto :fail )

copy /y "README.md" "%OUT%\" >nul

rmdir /s /q "%PUBLISH_TMP%"

rem ---------------------------------------------------------------- verify
for %%F in (BalatroTrainer.Gui.exe BalatroTrainer.Gui.dll BalatroTrainer.Gui.runtimeconfig.json BalatroTrainer.dll) do (
    if not exist "%OUT%\%%F" ( echo [ERROR] Missing in output: %%F & goto :fail )
)

echo.
echo ==== Done ====
echo Output folder: "%OUT%"
echo Run BalatroTrainer.Gui.exe after Balatro has started ^(keep BalatroTrainer.dll next to it^).
echo.
if /i not "%~1"=="nopause" pause
exit /b 0

:fail
echo.
echo ==== Build failed ====
if /i not "%~1"=="nopause" pause
exit /b 1
