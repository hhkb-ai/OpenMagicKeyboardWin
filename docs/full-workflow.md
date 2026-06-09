# Full Workflow

This document describes the full workflow for the OpenMagicKeyboardWin A2450 project.

Repository:

```text
https://github.com/hhkb-ai/OpenMagicKeyboardWin
```

## 1. Prepare the Windows development machine

Install:

- Git for Windows
- .NET 8 SDK
- Visual Studio 2022, optional for C# work
- Windows Driver Kit, only needed later for driver development

Verify:

```powershell
git --version
dotnet --version
```

## 2. Clone the repository

```powershell
git clone https://github.com/hhkb-ai/OpenMagicKeyboardWin.git
cd OpenMagicKeyboardWin
```

If you cloned the old repository URL before the rename, update the remote:

```powershell
git remote set-url origin https://github.com/hhkb-ai/OpenMagicKeyboardWin.git
```

## 3. Build the logger

```powershell
dotnet build .\tools\A2450HidLogger\A2450HidLogger.csproj -c Release
```

## 4. Run the logger

```powershell
dotnet run --project .\tools\A2450HidLogger\A2450HidLogger.csproj
```

A small window will open. Keep it open while testing keys.

Output files:

```text
logs/device-snapshot.json
logs/a2450-key-events.jsonl
```

## 5. Test sequence

Use the Apple Magic Keyboard A2450 over Bluetooth first.

Do not type passwords, private messages, or account information while the logger is running.

### Single keys

Press and release each key:

- Fn / Globe
- Left Ctrl
- Right Ctrl
- Command
- Option
- Backspace
- Arrow Up
- Arrow Down
- Arrow Left
- Arrow Right
- F1-F12
- Lock / Eject, if present

### Fn combinations

For each combination:

1. Hold Fn.
2. Press the target key.
3. Release the target key.
4. Release Fn.

Test:

- Fn + Left Ctrl
- Fn + F1-F12
- Fn + Backspace
- Fn + Arrow Up
- Fn + Arrow Down
- Fn + Arrow Left
- Fn + Arrow Right

### Left Ctrl combinations

For each combination:

1. Hold Left Ctrl.
2. Press the target key.
3. Release the target key.
4. Release Left Ctrl.

Test:

- Left Ctrl + F1-F12
- Left Ctrl + Backspace
- Left Ctrl + Arrow Up
- Left Ctrl + Arrow Down
- Left Ctrl + Arrow Left
- Left Ctrl + Arrow Right

## 6. Upload the logs

Upload these files for analysis:

```text
logs/device-snapshot.json
logs/a2450-key-events.jsonl
```

If the program fails, also provide:

- Terminal output
- Screenshot of the error
- Windows version
- Whether the keyboard is connected over Bluetooth or USB

## 7. Interpret the results

The key question is whether Fn appears in user-mode Raw Input.

Possible results:

1. Fn appears as a normal event.
2. Fn appears only indirectly by changing F-row or navigation key output.
3. Fn does not appear in Raw Input.
4. Fn appears only in vendor-defined HID data not captured by the first logger version.

If Fn does not appear, the next step is a lower-level HID reader or a driver-level prototype.

## 8. Driver design phase

Only start driver work after A2450 reports are collected.

Driver MVP:

- Bind only to A2450.
- Physical Fn emits Left Ctrl.
- Physical Left Ctrl becomes internal FnLayer.
- FnLayer + Backspace emits Delete.
- FnLayer + arrows emit Home, End, Page Up, Page Down.

## 9. Driver development caution

Driver development can break keyboard input if implemented incorrectly.

Before installing any prototype driver:

- Use a second keyboard or remote access fallback.
- Create a Windows restore point.
- Know how to boot into Safe Mode.
- Use test signing only on a development machine.
