# FlyCtl - Fast Command-Line Tool for FlyMenu

FlyCtl is a lightweight, high-performance command-line utility to control FlyMenu from external applications, mouse gestures, or keyboard shortcuts.

## Performance

FlyCtl is **10-20x faster** than AutoHotkey or PowerShell:
- **FlyCtl.exe**: ~5-10ms ?
- **PowerShell**: ~30-50ms
- **AutoHotkey**: ~50-100ms

## Usage

```cmd
FlyCtl.exe [command]
```

### Examples

```cmd
# Show menu at cursor position
FlyCtl.exe show

# Switch to next desktop
FlyCtl.exe "Next Desktop"

# Switch to previous desktop  
FlyCtl.exe "Prev Desktop"

# Execute any menu action by label
FlyCtl.exe "Desktop"

# Reload configuration
FlyCtl.exe reload

# Exit FlyMenu
FlyCtl.exe quit
```

## Installation

1. **Build from source:**
   ```cmd
   cd FlyCtl
   dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ../publish
   ```

2. **Copy to convenient location:**
   ```cmd
   copy publish\FlyCtl.exe .
   ```

3. **Or add to PATH** for system-wide access

## Requirements

- .NET 8.0 Runtime (already required by FlyMenu)
- Windows 10/11
- FlyMenu must be running

## Integration Examples

### Mouse Gesture Software
Bind `FlyCtl.exe show` to a mouse gesture (e.g., right-click + move up)

### Keyboard Shortcuts
Use AutoHotkey, PowerToys, or other tools to bind:
```autohotkey
^!m::Run, FlyCtl.exe show  ; Ctrl+Alt+M shows menu
```

### Task Scheduler
Create scheduled tasks that execute menu actions:
```cmd
FlyCtl.exe "Next Desktop"
```

## Technical Details

FlyCtl sends commands to FlyMenu using Windows `WM_COPYDATA` messages:
1. Finds the FlyMenu receiver window by enumerating all windows
2. Encodes the command as UTF-8 with null terminator
3. Sends via `SendMessage` API
4. Returns exit code 0 on success, 1 on failure

## Troubleshooting

**Error: "FlyMenu is not running"**
- Make sure FlyMenu.exe is running (check system tray)
- Verify the receiver window exists (it's created on FlyMenu startup)

**Command not working**
- Check that the command matches a menu label exactly (case-insensitive)
- Special commands: `show`, `reload`, `quit`, `exit`, `stop`
- Use quotes for multi-word commands: `FlyCtl.exe "Next Desktop"`

## Building Self-Contained

For a version that doesn't require .NET Runtime:

```cmd
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../publish-standalone
```

Note: This creates a larger executable (~60MB) but has no dependencies.

## Source Code

The source code is in `FlyCtl/Program.cs` - a single C# file with no external dependencies.
