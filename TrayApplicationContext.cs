using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsDesktop;
using System.IO;
using System.Runtime.InteropServices;

namespace FlyMenu
{
    /// <summary>
    /// Main application context for the FlyMenu tray application.
    /// Manages the tray icon, menu polling, and desktop tracking.
    /// </summary>
    public class TrayApplicationContext : ApplicationContext
    {
    private readonly NotifyIcon notifyIcon;
    private readonly ContextMenuStrip trayMenu;
        private readonly ContextMenuStrip flyoutMenu;
      private System.Windows.Forms.Timer pollTimer = null!;
   private MessageWindow? messageWindow;
        private readonly int uiThreadId;
     private MenuKeyboardFilter? keyboardFilter;
      private bool keyboardNavigationActive = false;

        // P/Invoke for setting focus and cursor
        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

[DllImport("user32.dll")]
      private static extern bool SetForegroundWindow(IntPtr hWnd);

     [DllImport("user32.dll")]
private static extern bool SetCursorPos(int X, int Y);

        public static VirtualDesktop?[] DesktopHistory = new VirtualDesktop?[2];

        public NotifyIcon NotifyIcon => notifyIcon;
  public ContextMenuStrip TrayMenu => trayMenu;
  public ContextMenuStrip FlyoutMenu => flyoutMenu;
        public System.Windows.Forms.Timer PollTimer { get => pollTimer; set => pollTimer = value; }

        public TrayApplicationContext()
        {
            System.Diagnostics.Debug.WriteLine("TrayApplicationContext: Initializing...");

   // Capture UI thread ID
            uiThreadId = Environment.CurrentManagedThreadId;
            System.Diagnostics.Debug.WriteLine($"TrayApplicationContext: UI Thread ID = {uiThreadId}");

            Application.ApplicationExit += OnApplicationExit;

      // Create hidden message window to receive WM_COPYDATA
     messageWindow = new MessageWindow(this);

     // Initialize tray menu
            trayMenu = new ContextMenuStrip();
      trayMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

      // Initialize tray icon with the custom kdf.ico from icons folder
         notifyIcon = new NotifyIcon
 {
       Icon = LoadTrayIcon(),
             ContextMenuStrip = trayMenu,
  Visible = true,
  Text = "FlyMenu"
            };

      notifyIcon.MouseClick += NotifyIcon_MouseClick;

            // Create the flyout menu container (items will be populated on demand)
         flyoutMenu = new ContextMenuStrip();
      flyoutMenu.Closed += FlyoutMenu_Closed;
  flyoutMenu.Opening += FlyoutMenu_Opening;

            // Subscribe to VirtualDesktop changes
         System.Diagnostics.Debug.WriteLine("TrayApplicationContext: Subscribing to VirtualDesktop.CurrentChanged...");
         VirtualDesktop.CurrentChanged += OnVirtualDesktopCurrentChanged;

 // Initialize VirtualDesktop library by querying current desktop
            try
{
      System.Diagnostics.Debug.WriteLine("TrayApplicationContext: Initializing VirtualDesktop library...");
        var current = VirtualDesktop.Current;
  var desktops = VirtualDesktop.GetDesktops();
   System.Diagnostics.Debug.WriteLine($"TrayApplicationContext: VirtualDesktop initialized. Current = {current?.Id}, Total desktops = {desktops.Length}");
    }
          catch (Exception ex)
     {
 System.Diagnostics.Debug.WriteLine($"TrayApplicationContext: VirtualDesktop initialization WARNING: {ex.GetType().Name}: {ex.Message}");
     }

         CreatePollTimer();
            System.Diagnostics.Debug.WriteLine("TrayApplicationContext: Initialization complete");
        }

        /// <summary>
        /// Loads the tray icon from the icons folder.
/// Falls back to SystemIcons.Application if icon file is not found.
   /// </summary>
        private static Icon LoadTrayIcon()
        {
            try
        {
      string iconPath = Path.Combine(AppContext.BaseDirectory, "icons", "kdf.ico");
      if (File.Exists(iconPath))
       {
        return new Icon(iconPath);
     }
      }
   catch (Exception ex)
            {
       System.Diagnostics.Debug.WriteLine($"Failed to load tray icon: {ex.Message}");
}

    // Fallback to default Windows icon if custom icon not found
            return SystemIcons.Application;
    }

  private void NotifyIcon_MouseClick(object? sender, MouseEventArgs e)
        {
       if (e.Button == MouseButtons.Left)
      {
           var cursor = Cursor.Position;
       var screen = Screen.FromPoint(cursor);
     var hotArea = ConfigLoader.GetHotAreaConfig();
                PopulateMenuFromConfig();
        ShowMenuAndSetupFocus(cursor, screen, cursor.Y, hotArea);
  }
        }

        /// <summary>
   /// Shows the menu and ensures focus is properly set
        /// </summary>
        private void ShowMenuAndSetupFocus(Point cursor, Screen screen, int yPosition, HotAreaConfig hotArea)
        {
            keyboardNavigationActive = false; // Reset on new menu show
       MenuUIHelper.ShowMenuCenteredUnderCursor(flyoutMenu, cursor, screen, yPosition, hotArea.Edge, hotArea.CatchMouse, hotArea.triggerHeight);
        
     // Defer focus setup to ensure menu is fully visible and has a window handle
         var focusTimer = new System.Windows.Forms.Timer { Interval = 50 };
    focusTimer.Tick += (s, e) =>
 {
     focusTimer.Stop();
                focusTimer.Dispose();
   SetMenuFocus();
     SelectFirstMenuItem();
            };
     focusTimer.Start();
        }

        /// <summary>
        /// Sets keyboard focus to the menu using its window handle
        /// </summary>
    private void SetMenuFocus()
      {
         try
     {
        // Get the menu's window handle using reflection
   var handleProperty = typeof(ToolStripDropDown).GetProperty("Handle", 
       System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
         
          if (handleProperty != null)
     {
             var handle = (IntPtr?)handleProperty.GetValue(flyoutMenu);
            if (handle.HasValue && handle.Value != IntPtr.Zero)
      {
  System.Diagnostics.Debug.WriteLine($"Setting focus to menu handle: 0x{handle.Value:X}");
           SetForegroundWindow(handle.Value);
                SetFocus(handle.Value);
   System.Diagnostics.Debug.WriteLine("Menu focus set successfully");
                return;
     }
       }
    
         System.Diagnostics.Debug.WriteLine("Could not get menu handle for focus");
    }
     catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting menu focus: {ex.Message}");
            }
      }

        private void PopulateMenuFromConfig()
        {
            var configs = ConfigLoader.LoadMenuConfigs();
            MenuBuilder.PopulateMenu(flyoutMenu, configs);
        }

     private void CreatePollTimer()
        {
            PollTimer = new System.Windows.Forms.Timer { Interval = 100 };
      PollTimer.Tick += PollTimer_Tick;
       PollTimer.Start();
        }

  private void PollTimer_Tick(object? sender, EventArgs e)
     {
var cursor = Cursor.Position;
      var screen = Screen.FromPoint(cursor);
            var hotArea = ConfigLoader.GetHotAreaConfig();

    // Update continuous mouse catching if enabled (only if not in keyboard mode)
            if (!keyboardNavigationActive)
          {
     MenuUIHelper.UpdateMouseCatch();
            }

    // Calculate bounds based on configured edge and percentages
  bool isInHotArea = IsInHotArea(cursor, screen, hotArea);

 // Show when cursor is in hot area
        if (isInHotArea)
 {
      if (!flyoutMenu.Visible)
    {
 PopulateMenuFromConfig();
  ShowMenuAndSetupFocus(cursor, screen, GetMenuYPosition(screen, hotArea), hotArea);
    }

      return;
  }

      // Hide menu if visible and cursor moves away from it
 // BUT: Don't auto-close if keyboard navigation is active
     if (flyoutMenu.Visible && !keyboardNavigationActive)
   {
     var bounds = flyoutMenu.Bounds;
         var padded = Rectangle.Inflate(bounds, 8, 8);
       if (!padded.Contains(cursor))
      {
           MenuUIHelper.DisableMouseCatch();
           flyoutMenu.Close();
        }
        }
        }

        /// <summary>
        /// Determines if cursor is in the configured hot area
     /// </summary>
        private static bool IsInHotArea(Point cursor, Screen screen, HotAreaConfig hotArea)
    {
         int tolerance = 3; // pixel tolerance
string edge = hotArea.Edge?.ToLowerInvariant() ?? "top";

            return edge switch
    {
      "top" => IsInTopHotArea(cursor, screen, hotArea, tolerance),
     "bottom" => IsInBottomHotArea(cursor, screen, hotArea, tolerance),
     "left" => IsInLeftHotArea(cursor, screen, hotArea, tolerance),
        "right" => IsInRightHotArea(cursor, screen, hotArea, tolerance),
   _ => false
     };
        }

        /// <summary>
        /// Checks if cursor is in top edge hot area
        /// </summary>
  private static bool IsInTopHotArea(Point cursor, Screen screen, HotAreaConfig hotArea, int tolerance)
        {
    var topEdge = screen.WorkingArea.Top;
   int screenWidth = screen.WorkingArea.Width;
   int leftBound = screen.WorkingArea.Left + (int)(screenWidth * hotArea.StartPercentage / 100.0);
            int rightBound = screen.WorkingArea.Left + (int)(screenWidth * hotArea.EndPercentage / 100.0);

    return cursor.Y <= topEdge + tolerance && cursor.X >= leftBound && cursor.X <= rightBound;
     }

        /// <summary>
 /// Checks if cursor is in bottom edge hot area
        /// </summary>
     private static bool IsInBottomHotArea(Point cursor, Screen screen, HotAreaConfig hotArea, int tolerance)
        {
        var bottomEdge = screen.WorkingArea.Bottom;
            int screenWidth = screen.WorkingArea.Width;
 int leftBound = screen.WorkingArea.Left + (int)(screenWidth * hotArea.StartPercentage / 100.0);
      int rightBound = screen.WorkingArea.Left + (int)(screenWidth * hotArea.EndPercentage / 100.0);

    return cursor.Y >= bottomEdge - tolerance && cursor.X >= leftBound && cursor.X <= rightBound;
}

        /// <summary>
        /// Checks if cursor is in left edge hot area
        /// </summary>
        private static bool IsInLeftHotArea(Point cursor, Screen screen, HotAreaConfig hotArea, int tolerance)
     {
   var leftEdge = screen.WorkingArea.Left;
      int screenHeight = screen.WorkingArea.Height;
     int topBound = screen.WorkingArea.Top + (int)(screenHeight * hotArea.StartPercentage / 100.0);
         int bottomBound = screen.WorkingArea.Top + (int)(screenHeight * hotArea.EndPercentage / 100.0);

            return cursor.X <= leftEdge + tolerance && cursor.Y >= topBound && cursor.Y <= bottomBound;
        }

        /// <summary>
        /// Checks if cursor is in right edge hot area
 /// </summary>
        private static bool IsInRightHotArea(Point cursor, Screen screen, HotAreaConfig hotArea, int tolerance)
        {
var rightEdge = screen.WorkingArea.Right;
            int screenHeight = screen.WorkingArea.Height;
            int topBound = screen.WorkingArea.Top + (int)(screenHeight * hotArea.StartPercentage / 100.0);
            int bottomBound = screen.WorkingArea.Top + (int)(screenHeight * hotArea.EndPercentage / 100.0);

            return cursor.X >= rightEdge - tolerance && cursor.Y >= topBound && cursor.Y <= bottomBound;
        }

        /// <summary>
        /// Gets the appropriate Y position for menu based on edge
        /// </summary>
        private static int GetMenuYPosition(Screen screen, HotAreaConfig hotArea)
 {
   string edge = hotArea.Edge?.ToLowerInvariant() ?? "top";
            return edge switch
      {
    "top" => screen.WorkingArea.Top,
      "bottom" => screen.WorkingArea.Bottom - 30, // Account for menu height
        "left" or "right" => Cursor.Position.Y,
          _ => screen.WorkingArea.Top
            };
        }

     /// <summary>
        /// Handles messages received via WM_COPYDATA from external applications
     /// </summary>
  internal void HandleReceivedMessage(string message)
        {
            try
            {
            // Check if we're on the UI thread
           int currentThreadId = Environment.CurrentManagedThreadId;
         System.Diagnostics.Debug.WriteLine($"HandleReceivedMessage called with: {message}");
         System.Diagnostics.Debug.WriteLine($"Current thread ID: {currentThreadId}, UI thread ID: {uiThreadId}");

                // Marshal to UI thread if needed
        if (currentThreadId != uiThreadId)
           {
  System.Diagnostics.Debug.WriteLine("Marshaling to UI thread...");
        trayMenu.BeginInvoke(new Action(() =>
    {
       System.Diagnostics.Debug.WriteLine($"Now executing on UI thread (ID: {Environment.CurrentManagedThreadId})");
          ProcessMessage(message);
          }));
             return;
   }

System.Diagnostics.Debug.WriteLine("Already on UI thread, processing message...");
                ProcessMessage(message);
 }
            catch (Exception ex)
 {
       System.Diagnostics.Debug.WriteLine($"Error handling message: {ex.Message}");
     System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

/// <summary>
  /// Processes the message on the UI thread (separated to avoid recursion)
        /// </summary>
   private void ProcessMessage(string message)
    {
  var callId = Guid.NewGuid().ToString().Substring(0, 8);
      System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: ENTRY - Message='{message}'");

         try
     {
          // Handle special built-in commands first
       var lowerMessage = message.Trim().ToLowerInvariant();

                if (lowerMessage == "quit" || lowerMessage == "exit")
      {
      System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: Quit command received");
     ExitApplication();
  return;
    }

 if (lowerMessage == "show")
      {
      System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: Show command received");
var deferTimer = new System.Windows.Forms.Timer { Interval = 1 };
     deferTimer.Tick += (s, e) =>
    {
         deferTimer.Stop();
            deferTimer.Dispose();
            var cursor = Cursor.Position;
 var screen = Screen.FromPoint(cursor);
          var hotArea = ConfigLoader.GetHotAreaConfig();
                 PopulateMenuFromConfig();
             ShowMenuAndSetupFocus(cursor, screen, cursor.Y, hotArea);
            };
      deferTimer.Start();
            return;
     }

 if (lowerMessage == "reload")
          {
        System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: Reload command received");
   ConfigLoader.ClearCache();
    System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: Config cache cleared");
     return;
           }

            // Parse regular menu actions
                var config = ParseMessageToConfig(message);

    if (config != null)
    {
                    System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: Config found: Type={config.Type}, Parameter={config.Parameter}");
  // Defer execution to avoid COM reentrancy issues (RPC_E_CANTCALLOUT_ININPUTSYNCCALL)
          // Use a timer to post the action after WM_COPYDATA processing completes
    var deferTimer = new System.Windows.Forms.Timer { Interval = 1 };
      deferTimer.Tick += (s, e) =>
              {
         deferTimer.Stop();
          deferTimer.Dispose();
           System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: Executing deferred menu action...");
         MenuActionHandler.ExecuteMenuAction(config);
               System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: Deferred menu action completed");
                 };
       deferTimer.Start();
  System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: Timer started");
                }
                else
                {
    System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: No matching config found for message");
   }
            }
      catch (Exception ex)
         {
     System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: ERROR - {ex.Message}");
     }

            System.Diagnostics.Debug.WriteLine($"ProcessMessage [{callId}]: EXIT");
        }

        /// <summary>
   /// Parses a received message string into a MenuItemConfig
        /// </summary>
        private static MenuItemConfig? ParseMessageToConfig(string message)
        {
 if (string.IsNullOrWhiteSpace(message))
  {
        System.Diagnostics.Debug.WriteLine("ParseMessageToConfig: Message is null or whitespace");
   return null;
            }

          message = message.Trim();
        System.Diagnostics.Debug.WriteLine($"ParseMessageToConfig: Parsing message '{message}'");

     var configs = ConfigLoader.LoadMenuConfigs();
    System.Diagnostics.Debug.WriteLine($"ParseMessageToConfig: Loaded {configs.Count} configs");

            foreach (var config in configs)
            {
                System.Diagnostics.Debug.WriteLine($"ParseMessageToConfig: Comparing with label '{config.Label}'");
           if (string.Equals(config.Label, message, StringComparison.OrdinalIgnoreCase))
    {
        System.Diagnostics.Debug.WriteLine($"ParseMessageToConfig: MATCH FOUND! Label='{config.Label}', Type='{config.Type}'");
  return config;
     }
  }

            System.Diagnostics.Debug.WriteLine("ParseMessageToConfig: No label match, checking direct action types...");
      
     // Handle action types directly
            var lowerMessage = message.ToLowerInvariant();
            System.Diagnostics.Debug.WriteLine($"ParseMessageToConfig: Normalized message = '{lowerMessage}'");
    
          if (lowerMessage is "switch left" or "switch right" or "switch before")
            {
         System.Diagnostics.Debug.WriteLine($"ParseMessageToConfig: Creating direct action config for '{lowerMessage}'");
           return new MenuItemConfig
   {
             Type = lowerMessage
          };
            }

   System.Diagnostics.Debug.WriteLine("ParseMessageToConfig: No match found, returning null");
      return null;
        }

        private void ExitApplication()
        {
            PollTimer?.Stop();
          PollTimer?.Dispose();

            messageWindow?.DestroyHandle();
            messageWindow = null;

            // Unregister keyboard filter
            if (keyboardFilter != null)
     {
           Application.RemoveMessageFilter(keyboardFilter);
            keyboardFilter = null;
}

     try
      {
     VirtualDesktop.CurrentChanged -= OnVirtualDesktopCurrentChanged;
            }
            catch { }

          NotifyIcon.Visible = false;
            NotifyIcon.Dispose();
         TrayMenu.Dispose();
        flyoutMenu.Dispose();

   Application.Exit();
        }

 private void OnApplicationExit(object? sender, EventArgs e)
        {
      ExitApplication();
        }

        private void OnVirtualDesktopCurrentChanged(object? sender, VirtualDesktopChangedEventArgs args)
  {
         try
            {
  System.Diagnostics.Debug.WriteLine("OnVirtualDesktopCurrentChanged: Desktop change detected");
    System.Diagnostics.Debug.WriteLine($"  Old Desktop: {args.OldDesktop?.Name} (ID: {args.OldDesktop?.Id})");
     System.Diagnostics.Debug.WriteLine($"  New Desktop: {args.NewDesktop?.Name} (ID: {args.NewDesktop?.Id})");

    DesktopHistory[0] = args.OldDesktop;
           DesktopHistory[1] = args.NewDesktop;
       var name = args.NewDesktop?.Name ?? "Unknown";

       System.Diagnostics.Debug.WriteLine($"Desktop history updated. History[0] = {DesktopHistory[0]?.Id}, History[1] = {DesktopHistory[1]?.Id}");
 //notifyIcon.Text = $"FlyMenu - Current Desktop: {name}";
    //notifyIcon.ShowBalloonTip(1000, "Desktop Changed", $"Switched to desktop: {name}", ToolTipIcon.Info);
       }
      catch (Exception ex)
  {
     System.Diagnostics.Debug.WriteLine($"OnVirtualDesktopCurrentChanged ERROR: {ex.GetType().Name}: {ex.Message}");
  System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

     protected override void Dispose(bool disposing)
        {
   if (disposing)
   {
                try
      {
               VirtualDesktop.CurrentChanged -= OnVirtualDesktopCurrentChanged;
     }
 catch { }

     messageWindow?.DestroyHandle();
    messageWindow = null;

if (keyboardFilter != null)
    {
           Application.RemoveMessageFilter(keyboardFilter);
          keyboardFilter = null;
                }

         PollTimer?.Stop();
      PollTimer?.Dispose();
       NotifyIcon?.Dispose();
          TrayMenu?.Dispose();
            flyoutMenu?.Dispose();
    }

        base.Dispose(disposing);
        }

    /// <summary>
    /// Called when the flyout menu is opening - sets up keyboard support
        /// </summary>
 private void FlyoutMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
        {
    System.Diagnostics.Debug.WriteLine("FlyoutMenu_Opening - setting up keyboard capture");
       
       // Install keyboard message filter if not already installed
       if (keyboardFilter == null)
{
 keyboardFilter = new MenuKeyboardFilter(flyoutMenu, this);
       Application.AddMessageFilter(keyboardFilter);
     System.Diagnostics.Debug.WriteLine("Keyboard filter installed");
   }

            // Add mouse move handler to detect when user returns to mouse mode
 flyoutMenu.MouseMove -= FlyoutMenu_MouseMove;
     flyoutMenu.MouseMove += FlyoutMenu_MouseMove;
        }

        /// <summary>
/// Handles mouse movement over the menu to switch back from keyboard mode
        /// </summary>
      private void FlyoutMenu_MouseMove(object? sender, MouseEventArgs e)
        {
       if (keyboardNavigationActive)
            {
      System.Diagnostics.Debug.WriteLine("Mouse movement detected, switching back to mouse mode");
                keyboardNavigationActive = false;
     }
    }

        /// <summary>
        /// Called when the flyout menu is closed - cleans up keyboard support
        /// </summary>
        private void FlyoutMenu_Closed(object? sender, ToolStripDropDownClosedEventArgs e)
  {
      System.Diagnostics.Debug.WriteLine("FlyoutMenu_Closed - cleaning up");
            keyboardNavigationActive = false; // Reset when menu closes
   // Note: We keep the filter registered for next menu show
        }

   /// <summary>
        /// Selects the first menu item
        /// </summary>
        private void SelectFirstMenuItem()
        {
        if (flyoutMenu.Items.Count > 0 && flyoutMenu.Items[0] is ToolStripMenuItem firstItem)
       {
                firstItem.Select();
  System.Diagnostics.Debug.WriteLine($"Auto-selected first item: {firstItem.Text}");
  }
        }

        /// <summary>
    /// Selects the next menu item and moves cursor away
        /// </summary>
        private void SelectNextMenuItem()
        {
   if (flyoutMenu.Items.Count == 0) return;

     keyboardNavigationActive = true;
            MoveCursorAwayFromMenu();

       int currentIndex = GetSelectedMenuItemIndex();
            int nextIndex = (currentIndex + 1) % flyoutMenu.Items.Count;
        
if (flyoutMenu.Items[nextIndex] is ToolStripMenuItem nextItem)
  {
 nextItem.Select();
    System.Diagnostics.Debug.WriteLine($"Selected menu item: {nextItem.Text}");
          }
        }

   /// <summary>
        /// Selects the previous menu item and moves cursor away
        /// </summary>
        private void SelectPreviousMenuItem()
     {
            if (flyoutMenu.Items.Count == 0) return;

       keyboardNavigationActive = true;
 MoveCursorAwayFromMenu();

   int currentIndex = GetSelectedMenuItemIndex();
        int prevIndex = currentIndex <= 0 ? flyoutMenu.Items.Count - 1 : currentIndex - 1;
    
       if (flyoutMenu.Items[prevIndex] is ToolStripMenuItem prevItem)
      {
   prevItem.Select();
                System.Diagnostics.Debug.WriteLine($"Selected menu item: {prevItem.Text}");
         }
  }

  /// <summary>
    /// Moves the mouse cursor away from the menu to prevent hover interference
        /// </summary>
        private void MoveCursorAwayFromMenu()
{
       try
      {
    if (!flyoutMenu.Visible) return;

        var menuBounds = flyoutMenu.Bounds;
    var screen = Screen.FromRectangle(menuBounds);
    
     // Move cursor to a safe position away from the menu
// Choose position based on where there's more space
    int newX, newY;
      
    if (menuBounds.Left > screen.WorkingArea.Width / 2)
              {
            // Menu is on right side, move cursor to the left
    newX = menuBounds.Left - 50;
         }
       else
       {
   // Menu is on left side, move cursor to the right
          newX = menuBounds.Right + 50;
}
       
      if (menuBounds.Top > screen.WorkingArea.Height / 2)
      {
           // Menu is on bottom, move cursor up
          newY = menuBounds.Top - 20;
    }
   else
      {
     // Menu is on top, move cursor down  
    newY = menuBounds.Bottom + 20;
       }

        // Clamp to screen bounds
            newX = Math.Max(screen.WorkingArea.Left, Math.Min(newX, screen.WorkingArea.Right - 1));
           newY = Math.Max(screen.WorkingArea.Top, Math.Min(newY, screen.WorkingArea.Bottom - 1));

    SetCursorPos(newX, newY);
                System.Diagnostics.Debug.WriteLine($"Moved cursor away from menu to ({newX}, {newY})");
  }
   catch (Exception ex)
       {
        System.Diagnostics.Debug.WriteLine($"Error moving cursor: {ex.Message}");
  }
        }

        /// <summary>
        /// Gets the index of the currently selected menu item
        /// </summary>
        private int GetSelectedMenuItemIndex()
        {
       for (int i = 0; i < flyoutMenu.Items.Count; i++)
            {
             if (flyoutMenu.Items[i] is ToolStripMenuItem item && item.Selected)
        {
  return i;
        }
   }
            return -1;
        }

   /// <summary>
    /// Executes the currently selected menu item
     /// </summary>
        private void ExecuteSelectedMenuItem()
        {
     for (int i = 0; i < flyoutMenu.Items.Count; i++)
            {
     if (flyoutMenu.Items[i] is ToolStripMenuItem item && item.Selected)
                {
 System.Diagnostics.Debug.WriteLine($"Executing menu item: {item.Text}");
        item.PerformClick();
    flyoutMenu.Close();
      return;
        }
       }

      System.Diagnostics.Debug.WriteLine("No menu item selected to execute");
        }

        // Public wrappers for MenuKeyboardFilter
 internal void SelectNextMenuItemPublic() => SelectNextMenuItem();
        internal void SelectPreviousMenuItemPublic() => SelectPreviousMenuItem();
        internal void ExecuteSelectedMenuItemPublic() => ExecuteSelectedMenuItem();
      internal void CloseMenuPublic()
        {
   keyboardNavigationActive = false;
     flyoutMenu.Close();
        }
    }

    /// <summary>
    /// Message filter to capture keyboard input for the menu
    /// </summary>
    internal class MenuKeyboardFilter : IMessageFilter
    {
        private const int WM_KEYDOWN = 0x0100;
   private const int WM_KEYUP = 0x0101;
   private const int WM_SYSKEYDOWN = 0x0104;
        private readonly ContextMenuStrip menu;
        private readonly TrayApplicationContext context;

        public MenuKeyboardFilter(ContextMenuStrip menu, TrayApplicationContext context)
      {
      this.menu = menu;
      this.context = context;
        }

        public bool PreFilterMessage(ref Message m)
        {
            // Only process if menu is visible and it's a key message
      if (!menu.Visible)
         return false;

    if (m.Msg != WM_KEYDOWN && m.Msg != WM_SYSKEYDOWN)
          return false;

 Keys key = (Keys)m.WParam.ToInt32() & Keys.KeyCode;
    Keys modifiers = Control.ModifierKeys;

            System.Diagnostics.Debug.WriteLine($"MenuKeyboardFilter: Key pressed = {key}, Modifiers = {modifiers}");

   // Handle navigation keys
            switch (key)
    {
                case Keys.Down:
     case Keys.Tab when modifiers == Keys.None:
          context.SelectNextMenuItemPublic();
     return true;

             case Keys.Up:
                case Keys.Tab when modifiers == Keys.Shift:
          context.SelectPreviousMenuItemPublic();
         return true;

                case Keys.Enter:
           case Keys.Space:
    context.ExecuteSelectedMenuItemPublic();
     return true;

                case Keys.Escape:
       context.CloseMenuPublic();
      return true;

  case Keys.Home:
         // Select first item
      if (menu.Items.Count > 0 && menu.Items[0] is ToolStripMenuItem firstItem)
         {
 firstItem.Select();
  }
            return true;

      case Keys.End:
   // Select last item
        if (menu.Items.Count > 0 && menu.Items[menu.Items.Count - 1] is ToolStripMenuItem lastItem)
      {
lastItem.Select();
       }
       return true;
   }

            return false;
 }
    }

    /// <summary>
    /// Hidden window that receives WM_COPYDATA messages from external applications like AutoHotkey
    /// </summary>
    internal class MessageWindow : NativeWindow
  {
        private const int WM_COPYDATA = 0x004A;
        private readonly TrayApplicationContext context;

      [StructLayout(LayoutKind.Sequential)]
   private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
  public int cbData;
        public IntPtr lpData;
        }

        public MessageWindow(TrayApplicationContext context)
        {
     this.context = context;
       CreateHandle(new CreateParams
{
          Caption = "FlyMenuReceiverWindow",
     Parent = IntPtr.Zero,
         Style = 0
   });
 System.Diagnostics.Debug.WriteLine($"MessageWindow created with handle: 0x{Handle:X}");
        }

 protected override void WndProc(ref Message m)
        {
if (m.Msg == WM_COPYDATA)
      {
           System.Diagnostics.Debug.WriteLine($"WM_COPYDATA received!");
      try
    {
             var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(m.LParam);
               if (cds.cbData > 0 && cds.lpData != IntPtr.Zero)
             {
      string message = Marshal.PtrToStringUTF8(cds.lpData, cds.cbData - 1) ?? string.Empty;
    System.Diagnostics.Debug.WriteLine($"Message content: '{message}'");
             context.HandleReceivedMessage(message);
 m.Result = (IntPtr)1; // Return 1 to indicate success
      return;
           }
              else
  {
          System.Diagnostics.Debug.WriteLine($"Invalid COPYDATASTRUCT: cbData={cds.cbData}, lpData=0x{cds.lpData:X}");
         }
     }
       catch (Exception ex)
      {
 System.Diagnostics.Debug.WriteLine($"Error in WndProc: {ex.Message}");
        System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
     }
    }

      base.WndProc(ref m);
}
 }
}
