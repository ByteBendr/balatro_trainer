# Balatro Trainer - Steam Version

WinForms GUI (C#) + injected C++ backend. Buttons: money add/set, hands, discards, hand size,
joker slots, consumable slots, round score, "meet blind target".

## How it works

```
BalatroTrainer.Gui.exe  --LoadLibraryW injection-->  Balatro.exe
        |                                                 |
        |   named pipe \\.\pipe\BalatroTrainer            |  BalatroTrainer.dll
        +------------- "money_add 100\n" ---------------> |   - MinHook on lua_pushnumber (lua51.dll)
                                                          |     only to catch the game's lua_State
                                                          |   - wraps love.update; drains the command
                                                          |     queue on the game thread each frame
```

* The pipe only accepts a fixed whitelist of command names plus one integer. No Lua travels over it.
* Commands only run while you are inside a run; otherwise the log says so.

## Build (Windows, x64)

Requirements: Visual Studio Build Tools (C++ workload), CMake, .NET SDK (10, or change the
`TargetFramework` in the csproj to `net8.0-windows`).

```powershell
# 1) backend DLL  (downloads MinHook v1.3.4 from GitHub at configure time)
cd native
cmake -B build -A x64
cmake --build build --config Release        # -> native\build\Release\BalatroTrainer.dll

# 2) GUI  (copies the DLL next to the exe automatically)
cd ..\gui
dotnet build -c Release                     # -> gui\bin\Release\net10.0-windows\
```

## Use

1. Start Balatro from Steam.
2. Run `BalatroTrainer.Gui.exe`. It finds `Balatro.exe`, injects the DLL, and shows "Connected - ready"
   once the game has finished loading.
3. Start or continue a run, then press buttons.

Log file: `%TEMP%\BalatroTrainer.log` (also reachable with the "Open log" button).

## Status / caveats (please read)

* Antivirus may flag the DLL injector. It is a generic technique, and you built it yourself, so
  allow-list the folder if needed.
* Keep a backup of `%AppData%\Balatro` before first use.
