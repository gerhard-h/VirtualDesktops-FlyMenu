# VirtualDesktops-FlyMenu

A customizable fly-out menu triggered by (parts) of the screen edges or by hotkey or by contol program.

This is a more conceized and faster alternative to Windows WIN+TAB functionality, for switching desktops and applications.
It also works while in full-screen remote desktop sessions.
It also offers a launchpad for programs and for executing keyboard shortcuts.


**NEW**: FlyCtl is now a high-performance command-line tool for controlling FlyMenu from external applications.
(FlyMenu.exe is running in the background to offer fast Desktop switching )

### Performance Comparison
- **FlyCtl.exe**: ~5-10ms ⚡ (Recommended)
- **flyctl.ps1** (PowerShell): ~30-50ms
- **flyctl.ahk** (AutoHotkey): ~50-100ms

## Shortcut / Taskbar / .lnk File support
With the included `FlyCtl.exe` or legacy `flyctl.ahk` script you can create shortcuts
to desktop actions (or any other actions) on the taskbar.
**Recommended**: `FlyCtl.exe "Desktop 2"` (fast)  
**Legacy**: `autohotkey flyctl.ahk "Desktop 2"` (slower)

## activation per mouse button 
Bind `FlyCtl.exe show` or just `FlyCtl.exe` to any mouse button or gesture as an alternative activation method 
to screen edge triggers. If your mouse can not run executables, create a link/shortcut to flyctl and define a keyboard shortcut to that .lnk. But keep in mind the FlyMenu itself does not support keyboard input. 


`flyctl` accepts any menu label as parameter. Special commands: default=`show`, `reload`

```
┌──────────┌────────────▼────────┐┌──────────────┐──────────────────────┐
│      │ ▶ Latest Desktop    ││ Dock Desktop 1
│  │ ▶ Desktop 1  ││ app1   
│    │ ▶ Desktop 2         ││ app2
│        │ ▶ Desktop n         ││Dock Desktop 2
│        ├─────────────────────┤│ app 3       
│          │ ▶ Next Desktop      │└──────────────┘
│          │ ▶ Show Desktop      │
│     │ ▶ Screenshot        │
│          │ ▶ Win+Tab           │      
│      │ ▶ Run notepad++     │  
││ ▶ ...     │
│          └─────────────────────┘  
│       
│  
│          ┌─┐┌─┐┌─┐┌─┐
│          │⊞││1││2││3│
└──────────└─┘└─┘└─┘└─┘────────────────────────────────────────────────┘
```

## 🌟 Features
- **🖱️ Edge-Triggered Menu** - Hover your mouse at any screen edge (top, bottom, left, right) to instantly reveal the menu
- **🖥️ Virtual Desktop Management** - Switch between Windows virtual desktops seamlessly
  - Switch to last used desktop
  - Switch desktop left/right with rollover
  - Direct access to all desktops by name
- ** ALT-Tab like Dock sortable by name or last used
- **🎯 Launch programs
- **📍 Run keyboard shortcuts
- **📍 Smart Positioning** - Menu appears centered under cursor with first menu item highlighted
- **💾 JSON Configuration** - Easy-to-edit configuration
- **🎨 Icon Support** - Custom icons for menu items
- **⚡ Fast External Control** - FlyCtl.exe for sub-10ms response time

## 📋 Requirements/ Tested Environment

-  Windows 11 (2025-12-23 or newer)
- .NET 8.0 Runtime (or newer) 
- Virtual Desktop feature enabled
- For mouse gestures: Use `FlyCtl.exe show`

## Motivation (why not uses AutoHotkey + VirtualDesktopAccessor.dll)
AutoHotkey can do the same and Menus have keyboard support out of the box. 
But since menus in AutoHotkey are modal they won't autoclose after loosing mouse focus 
wich is essential if the menu is triggered by mouse near screen edge.


## 🚀 Installation

### Option 1: Build from Source (Recommended)

```bash
# Clone the repository
git clone https://github.com/gerhard-h/VirtualDesktops-FlyMenu.git
cd VirtualDesktops-FlyMenu

# Build FlyMenu
dotnet build -c Release

# Build FlyCtl command-line tool
cd FlyCtl
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ../publish
cd ..

# Run
bin\Release\net8.0\FlyMenu.exe
```

### Option 2: Quick Start

```bash
dotnet run
```

## ⚙️ Configuration

FlyMenu is configured through the `FlyMenu.config` JSON file in the application directory.
Details are within the .config file itself.


## 📝 Menu Item Types

### Virtual Desktop Actions

| `switch before` | Switch to last visited desktop |
| `switch to` | Switch to specific desktop GUID | Direct desktop switching |
| `switch right` | Cycles right with rollover |
| `switch left` | Cycles left with rollover |

### Other Actions
| `run` | Execute program/command like `"notepad.exe"` or `"sharex -workflow capture"` |
| `shortcut` | Execute keyboard shortcut the syntax resembles the syntax of the library used  `"ModifiedKeyStroke(LWIN, VK_D)"` |
| `exit` | does nothing besides closeing the menu |

### Shortcut Format

Warning: LALT is called LMENU in this library

Two formats are supported:

#### 1. Single Key Press
```json
{
  "type": "shortcut",
"parameter": "KeyPress(F3)"
}
```

#### 2. Modified Key Stroke
```json
{
  "type": "shortcut",
  "parameter": "ModifiedKeyStroke(LWIN, VK_D)"
}
```

**Format**: `ModifiedKeyStroke(modifiers, keys)`
- **Modifiers** (space-separated): `LWIN`, `RWIN`, `LCONTROL`, `RCONTROL`, `LMENU`, `RMENU`, `LSHIFT`, `RSHIFT`
- **Keys** (space-separated): Any virtual key code (e.g., `VK_D`, `TAB`, `F3`, `ESCAPE`)

**Examples**:
- `ModifiedKeyStroke(LWIN, VK_D)` - Windows + D (Show Desktop)
- `ModifiedKeyStroke(LWIN, TAB)` - Windows + Tab (Task View)
- `ModifiedKeyStroke(LCONTROL LALT, VK_N)` - Ctrl + Alt + N
- `ModifiedKeyStroke(LWIN LSHIFT, VK_T)` - Win + Shift + T

### Special Placeholder

**`DESKTOP LIST`** - Automatically expands to show all available virtual desktops:

```json
{
  "label": "DESKTOP LIST",
  "type": "switch to",
  "parameter": ""
}
```

This single entry will be replaced at runtime with individual menu items for each virtual desktop.

## 🎨 Icons

Icons are loaded from the `icons` subfolder in the application directory.

### Icon Configuration
```json
{
  "icon": "preferences-desktop-wallpaper-2.ico"
}
```

### Icon Search Order
1. `icons/` subfolder (recommended)
2. Application root directory (fallback)
3. Absolute or relative paths (if directory specified)

### Supported Formats
- `.ico` - Windows icon format

## 🔧 Advanced Features

### Mouse Catching

When `catchMouse` is enabled, the cursor is prevented from sliding off the menu:

- **Top edge**: Cursor is pushed down into the menu
- **Bottom edge**: Cursor is pushed up into the menu
- **Left edge**: Cursor is pushed right into the menu
- **Right edge**: Cursor is pushed left into the menu

The `catchHeight` parameter (default: 10 pixels) determines how aggressively the cursor is repositioned.

### JSON Comments

FlyMenu supports JSON with comments and trailing commas:

```json
{
  "hotArea": {
    "edge": "top",  // This is a comment
 "startPercentage": 15,  /* This is also a comment */
  }  // Trailing comma is OK
}
```

### Dynamic Desktop List

The `DESKTOP LIST` placeholder automatically updates when:
- Virtual desktops are added or removed
- Desktop names are changed
- Menu is reopened

## 🛠️ Development

### Building from Source

```bash
# Clone the repository
git clone https://github.com/gerhard-h/VirtualDesktops-FlyMenu.git
cd VirtualDesktops-FlyMenu

# Build the project
dotnet build

# Run the application
dotnet run
```

### Dependencies

- **[Slions.VirtualDesktop](https://www.nuget.org/packages/Slions.VirtualDesktop/)** - Virtual Desktop API wrapper
- **[InputSimulatorStandard](https://www.nuget.org/packages/InputSimulatorStandard/)** - Keyboard input simulation


## 🐛 Troubleshooting

### Menu doesn't appear
- Check that the hot area percentages are valid (0-100)
- Verify the edge is set to a valid value: "top", "bottom", "left", or "right"
- Ensure you're moving the cursor to the correct screen edge

### Hotkey doesn't work
- Check if another application is using the same hotkey
- Try a different key combination
- Verify the hotkey format is correct (e.g., `"CTRL+ALT+M"`)

### Icons don't display
- Verify icon files exist in the `icons` folder
- Check that icon paths in config match actual filenames
- Ensure icons are in `.ico` format

### Shortcuts don't execute
- Verify the shortcut parameter format is correct
- Check that virtual key codes are valid
- Review the debug output for error messages

## 📜 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🙏 Acknowledgments

- [Slions.VirtualDesktop](https://github.com/Slion/Slions.VirtualDesktop) - Virtual Desktop API
- [InputSimulatorStandard](https://github.com/GregsStack/InputSimulatorStandard) - Keyboard simulation

## 📞 No Support
The code works fine, but is not regularly maintained. Use at your own risk.

- **GitHub**: [https://github.com/gerhard-h/VirtualDesktops-FlyMenu](https://github.com/gerhard-h/VirtualDesktops-FlyMenu)


---
