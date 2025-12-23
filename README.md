# VirtualDesktops-FlyMenu

A customizable fly-out menu triggered by (parts) of the screen edges.

This is a more conceized,faster alternative to Windows WIN+TAB functionality, for switching desktops.
It also works while in full-screen remote desktop sessions.
It can also or exclusivly be used as a launchpad vor programs or to execute keyboard shortcuts.

```
┌──────────┌────────────▼────────┐─────────────────────────────────────┐
│          │ ▶ Latest Desktop    │
│          │ ▶ Desktop 1         │  
│          │ ▶ Desktop 2         │
│          │ ▶ Desktop n         │
│          ├─────────────────────┤       
│          │ ▶ Next Desktop      │   
│          │ ▶ Show Desktop      │
│          │ ▶ Screenshot        │
│          │ ▶ Win+Tab           │      
│          │ ▶ Run notepad++     │    
│          │ ▶      ...          │
│          └─────────────────────┘  
│       
│  
│     
│         
└──────────────────────────────────────────────────────────────────────┘
```

## 🌟 Features

- **🖱️ Edge-Triggered Menu** - Hover your mouse at any screen edge (top, bottom, left, right) to instantly reveal the menu
- **🖥️ Virtual Desktop Management** - Switch between Windows virtual desktops seamlessly
  - Switch to last used desktop
  - Switch desktop left/right with rollover
  - Direct access to all desktops by name
- **🎯 Launch programs
- **📍 Run keyboard shortcuts
- **📍 Smart Positioning** - Menu appears centered under cursor with first menu item highlighted
- **💾 JSON Configuration** - Easy-to-edit configuration with JSON comments support
- **🎨 Icon Support** - Custom icons for menu items

## 📋 Requirements/ Tested Environment

-  Windows 11 (2025-12-23 or newer)
- .NET 8.0 Runtime 
- Virtual Desktop feature enabled

## 🚀 Installation

You have to build the project from source. 


## ⚙️ Configuration

FlyMenu is configured through the `FlyMenu.config` JSON file in the application directory.

### Hot Area Configuration

Control where and how the menu triggers:

```json
{
  "hotArea": {
    "edge": "top",       /* Screen edge: "top", "bottom", "left", or "right" */
    "startPercentage": 15,      /* Start position along edge (0-100%) */
    "endPercentage": 45,     /* End position along edge (0-100%) */
    "catchMouse": true,         /* Keep mouse from sliding off menu */
    "catchHeight": 10,          /* Pixels to reset mouse position */
  }
}
```

#### Edge Options
- **`"top"`** - Top edge of screen (most common)
- **`"bottom"`** - Bottom edge of screen
- **`"left"`** - Left edge of screen
- **`"right"`** - Right edge of screen

#### Percentage Range
- **0-100%** - Define the active portion of the screen edge
- Example: `15-45%` creates a trigger zone in the center-left portion
- Example: `0-100%` uses the entire edge

### Styling Configuration

Customize the menu appearance:

```json
{
  "styling": {
    "fontName": "Segoe UI",     /* Font family name */
    "fontSize": 11/* Font size in points */
  }
}
```

### Menu Items

Define menu actions:

```json
{
  "menuItems": [
    {
      "label": "Desktop List",
      "type": "switch to",
      "parameter": "%id%",
      "icon": "desktop.ico"
 }
  ]
}
```

## 📝 Menu Item Types

### Virtual Desktop Actions

| Type | Description | Parameter | Example |
|------|-------------|-----------|---------|
| `switch before` | Switch to previous desktop | Not used | Switch to last visited desktop |
| `switch to` | Switch to specific desktop | Desktop GUID | Direct desktop switching |
| `switch right` | Switch to next desktop | Not used | Cycles right with rollover |
| `switch left` | Switch to previous desktop | Not used | Cycles left with rollover |

### System Actions

| Type | Description | Parameter | Example |
|------|-------------|-----------|---------|
| `run` | Execute program/command | Command line | `"notepad.exe"` or `"sharex -workflow capture"` |
| `shortcut` | Execute keyboard shortcut | Shortcut format | `"ModifiedKeyStroke(LWIN, VK_D)"` |
| `exit` | Exit FlyMenu | Not used | Exit application |

### Shortcut Format

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
- **Modifiers** (space-separated): `LWIN`, `RWIN`, `LCONTROL`, `RCONTROL`, `LALT`, `RALT`, `LSHIFT`, `RSHIFT`
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
  "parameter": "%id%"
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

## 💡 Usage Examples

### Example 1: Top Edge Menu (Default)
```json
{
  "hotArea": {
    "edge": "top",
    "startPercentage": 0,
    "endPercentage": 100,
    "hotkey": "CTRL+ALT+M"
  }
}
```
Move mouse to top of scree

### Example 2: Corner Trigger
```json
{
  "hotArea": {
    "edge": "top",
    "startPercentage": 0,
    "endPercentage": 20
  }
}
```
Only triggers in top-left corner (first 20% of screen width)

### Example 3: Right Edge Menu
```json
{
  "hotArea": {
    "edge": "right",
    "startPercentage": 30,
    "endPercentage": 70
  }
}
```
Triggers on right edge, middle 40% of screen height

### Example 4: Comprehensive Menu
```json
{
  "menuItems": [
    {
      "label": "Previous Desktop",
      "type": "switch before",
   "icon": "arrow-left.ico"
    },
    {
      "label": "DESKTOP LIST",
      "type": "switch to",
      "parameter": "%id%"
    },
    {
      "label": "Next Desktop",
 "type": "switch right",
      "icon": "arrow-right.ico"
    },
    {
 "label": "Show Desktop",
      "type": "shortcut",
      "parameter": "ModifiedKeyStroke(LWIN, VK_D)",
      "icon": "desktop.ico"
    },
    {
      "label": "Task View",
      "type": "shortcut",
   "parameter": "ModifiedKeyStroke(LWIN, TAB)",
      "icon": "task-view.ico"
    },
    {
      "label": "Screenshot",
      "type": "run",
      "parameter": "snippingtool.exe",
      "icon": "screenshot.ico"
    },
    {
      "label": "Exit FlyMenu",
      "type": "exit"
 }
  ]
}
```

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

### Project Structure

```
FlyMenu/
├── FlyMenu.csproj       # Project file
├── Program.cs  # Application entry point
├── TrayApplicationContext.cs   # Main application logic
├── ConfigLoader.cs             # Configuration loading
├── HotAreaConfig.cs            # Configuration models
├── MenuBuilder.cs              # Menu construction
├── MenuUIHelper.cs        # UI helpers and positioning
├── MenuActionHandler.cs        # Action execution
├── KeyboardHelper.cs    # Keyboard shortcut handling
├── GlobalHotKey.cs   # Global hotkey registration
├── HotkeyMessageWindow.cs      # Hotkey message handling
├── FlyMenu.config    # Configuration file
└── icons/           # Icon directory
```

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
