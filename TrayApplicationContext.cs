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
        private readonly ContextMenuStrip appMenu;  // New: App menu
        private System.Windows.Forms.Timer pollTimer = null!;
        private MessageWindow? messageWindow;
        private readonly List<HotAreaIndicator> hotAreaIndicators = new List<HotAreaIndicator>();
        private PinnedBarWindow? pinnedBar;
        private readonly int uiThreadId;

        // Remembered state of the most recent ShowMenus() call so that
        // MenuActionHandler can reopen the menu at the same position when a
        // menu item is marked keepOpen == true.
        private Point lastShowCursor;
        private Screen? lastShowScreen;
        private int lastShowYPosition;
        private HotAreaConfig? lastShowHotArea;
        private bool hasLastShow;

        // Static hook installed by the constructor so static helpers
        // (MenuActionHandler) can request a reopen without a direct reference.
        internal static Action? ReopenLastMenus;

        public static VirtualDesktop?[] DesktopHistory = new VirtualDesktop?[2];

        public NotifyIcon NotifyIcon => notifyIcon;
        public ContextMenuStrip TrayMenu => trayMenu;
        public ContextMenuStrip FlyoutMenu => flyoutMenu;
        public ContextMenuStrip AppMenu => appMenu;  // New: Expose app menu
        public System.Windows.Forms.Timer PollTimer { get => pollTimer; set => pollTimer = value; }

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

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
            flyoutMenu.Closed += (s, e) => { /* no-op - poller controls show/hide */ };

            // Create the app menu container (items will be populated on demand)
            appMenu = new ContextMenuStrip();
            appMenu.Closed += (s, e) => { /* no-op - poller controls show/hide */ };

            // Set menu references in MenuActionHandler so actions can close menus
            MenuActionHandler.SetMenuReferences(flyoutMenu, appMenu);

            // Provide a reopen hook for keepOpen menu items
            ReopenLastMenus = ReopenLastShownMenus;

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

            // Create the hot-area visual indicator (click-through, non-activating overlay)
            SyncHotAreaIndicators(ConfigLoader.GetHotAreaConfig());

            // Pinned-bar is created on-demand in ShowPinnedBar and destroyed in
            // DestroyPinnedBar (called when the combined menu bounds are left).

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
                ShowMenus(cursor, screen, cursor.Y, hotArea);
            }
        }

        /// <summary>
        /// Shows both the flyout menu and app menu (if enabled) side by side with zero gap
        /// </summary>
        private void ShowMenus(Point cursor, Screen screen, int yPosition, HotAreaConfig hotArea, bool moveCursor = true)
        {
            // Remember args so "keepOpen" actions can reopen the menu at the same spot.
            lastShowCursor = cursor;
            lastShowScreen = screen;
            lastShowYPosition = yPosition;
            lastShowHotArea = hotArea;
            hasLastShow = true;
            // Populate desktop menu
            PopulateMenuFromConfig();

            // Check if app menu should be shown
            bool showAppMenu = ConfigLoader.GetShowAppMenu();

            // Reserve vertical space for the pinned bar so it doesn't cover the
            // menu it sits above. The flyout stays at the very top of the screen;
            // only the anchor used to place other things (app menu / pinned bar)
            // is offset downward.
            int pinnedBarReserve = MeasurePinnedBarHeight();

            if (showAppMenu)
            {
                // Populate app menu
                AppMenuBuilder.PopulateAppMenu(appMenu);

                // CRITICAL FIX: Disable AutoClose temporarily to prevent Windows Forms from
                // automatically closing flyoutMenu when appMenu.Show() is called
                flyoutMenu.AutoClose = false;
                appMenu.AutoClose = false;

                // Show flyout menu first to get its width
                MenuUIHelper.ShowMenuCenteredUnderCursor(flyoutMenu, cursor, screen, yPosition, hotArea.Edge, hotArea.CatchMouse, hotArea.triggerHeight, moveCursor);

                // TASKBAR FIX: Prevent menu from appearing in taskbar
                PreventTaskbarAppearance(flyoutMenu);

                var flyoutBounds = flyoutMenu.Bounds;
                var work = screen.WorkingArea;

                // Determine app-menu size before showing so we can decide the side
                var appPreferred = appMenu.GetPreferredSize(Size.Empty);
                int appMenuWidth = Math.Max(1, appPreferred.Width);
                int appMenuHeight = Math.Max(1, appPreferred.Height);

                // Prefer right side (zero gap). If it does not fit, place on the left instead.
                int appMenuX;
                if (flyoutBounds.Right + appMenuWidth <= work.Right)
                {
                    appMenuX = flyoutBounds.Right;
                }
                else if (flyoutBounds.Left - appMenuWidth >= work.Left)
                {
                    appMenuX = flyoutBounds.Left - appMenuWidth;
                }
                else
                {
                    // Neither side fits fully - clamp to right side of screen
                    appMenuX = Math.Max(work.Left, work.Right - appMenuWidth);
                }

                // Align vertically with flyout, plus reserve room for the pinned bar
                // (the bar sits above the app menu, so push the app menu down by the
                // bar's height). Flyout is intentionally NOT shifted.
                int appMenuY = flyoutBounds.Top + pinnedBarReserve;
                if (appMenuY + appMenuHeight > work.Bottom)
                    appMenuY = Math.Max(work.Top, work.Bottom - appMenuHeight);
                // Show with an explicit direction so WinForms does not auto-flip
                // horizontally (which caused it to land on top of the flyout when
                // invoked from the tray icon in the bottom-right corner).
                appMenu.Show(new Point(appMenuX, appMenuY), ToolStripDropDownDirection.BelowRight);

                // TASKBAR FIX: Prevent app menu from appearing in taskbar
                PreventTaskbarAppearance(appMenu);

                // Position optional pinned bar directly above the app menu
                ShowPinnedBar(screen, appMenu.Bounds);

                System.Diagnostics.Debug.WriteLine($"ShowMenus: Flyout at ({flyoutBounds.X}, {flyoutBounds.Y}) size {flyoutBounds.Size}, App at ({appMenuX}, {appMenuY}) size ({appMenuWidth}, {appMenuHeight})");
            }
            else
            {
                // Show only flyout menu. When a pinned bar is enabled, the bar
                // sits above the flyout, so push the flyout down by the bar's
                // height to keep the bar within the screen and above the menu.
                int flyoutY = yPosition + pinnedBarReserve;
                MenuUIHelper.ShowMenuCenteredUnderCursor(flyoutMenu, cursor, screen, flyoutY, hotArea.Edge, hotArea.CatchMouse, hotArea.triggerHeight, moveCursor);

                // TASKBAR FIX: Prevent menu from appearing in taskbar
                PreventTaskbarAppearance(flyoutMenu);

                // Pinned bar (if enabled) anchors above the flyout when there is no app menu
                ShowPinnedBar(screen, flyoutMenu.Bounds);
            }
        }

        /// <summary>
        /// Returns the height (in pixels) the pinned bar would occupy if shown
        /// with the current config, or 0 if the bar is disabled/empty. Used to
        /// reserve vertical space above the menus so the bar does not cover them.
        /// </summary>
        private static int MeasurePinnedBarHeight()
        {
            var cfg = ConfigLoader.GetPinnedBarConfig();
            if (cfg == null || !cfg.Enabled || cfg.Items == null || cfg.Items.Count == 0)
                return 0;
            int size = Math.Max(16, Math.Min(128, cfg.IconSize));
            return size + Math.Max(0, cfg.PaddingTop) + Math.Max(0, cfg.PaddingBottom);
        }

        /// <summary>
        /// Creates a fresh pinned bar window, populates it from config, and
        /// positions it directly above the given anchor (typically appMenu.Bounds
        /// or flyoutMenu.Bounds), left-aligned to it. Any previous bar instance
        /// is destroyed first so we never fight WinForms/Win32 state left over
        /// from a hidden window.
        /// </summary>
        private void ShowPinnedBar(Screen screen, Rectangle anchor)
        {
            // Always destroy any existing bar so each activation starts from a
            // fresh HWND. Cheap (few icons, few buttons) and avoids the
            // WS_EX_NOACTIVATE + WS_EX_TOPMOST re-show quirks we hit before.
            DestroyPinnedBar();

            var cfg = ConfigLoader.GetPinnedBarConfig();
            bool enabled = cfg != null && cfg.Enabled && cfg.Items != null && cfg.Items.Count > 0;
            if (!enabled) return;

            pinnedBar = new PinnedBarWindow();
            pinnedBar.Rebuild(cfg!);

            var work = screen.WorkingArea;

            int barX = anchor.Left;
            int barY = anchor.Top - pinnedBar.Height;
            if (barY < work.Top) barY = work.Top;
            if (barX + pinnedBar.Width > work.Right)
                barX = Math.Max(work.Left, work.Right - pinnedBar.Width);

            pinnedBar.ShowNoActivate(new Point(barX, barY));
        }

        /// <summary>Destroys the current pinned bar instance if any.</summary>
        private void DestroyPinnedBar()
        {
            if (pinnedBar != null)
            {
                try { pinnedBar.Dispose(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"DestroyPinnedBar: {ex.Message}"); }
                pinnedBar = null;
            }
        }

        private void PopulateMenuFromConfig()
        {
            var configs = ConfigLoader.LoadMenuConfigs();
            MenuBuilder.PopulateMenu(flyoutMenu, configs);
        }

        /// <summary>
        /// Reopens the flyout (and app menu) at the same position as the last
        /// ShowMenus() call. Used for menu items with keepOpen == true.
        /// Called on the UI thread from a menu Click handler; a short timer
        /// defer lets the current action (e.g. VirtualDesktop.Switch) and the
        /// menu-close side effects settle before we reopen.
        /// </summary>
        private void ReopenLastShownMenus()
        {
            if (!hasLastShow || lastShowScreen == null || lastShowHotArea == null)
            {
                System.Diagnostics.Debug.WriteLine("ReopenLastShownMenus: no remembered state, skipping");
                return;
            }

            try
            {
                var t = new System.Windows.Forms.Timer { Interval = 30 };
                t.Tick += (s, e) =>
                {
                    t.Stop();
                    t.Dispose();
                    try
                    {
                        if (!hasLastShow || lastShowScreen == null || lastShowHotArea == null)
                            return;

                        // Make sure both menus are actually closed before re-showing;
                        // otherwise ContextMenuStrip.Show becomes a no-op.
                        if (flyoutMenu.Visible) flyoutMenu.Close();
                        if (appMenu.Visible) appMenu.Close();

                        // Re-enable AutoClose so the reopened menu behaves normally
                        flyoutMenu.AutoClose = true;
                        appMenu.AutoClose = true;

                        // Use the last-known cursor rather than the (potentially moved)
                        // current cursor so the menu appears at the same spot.
                        // moveCursor=false: keep the pointer where the user clicked so
                        // subsequent clicks land on the same item.
                        ShowMenus(lastShowCursor, lastShowScreen, lastShowYPosition, lastShowHotArea, moveCursor: false);
                        System.Diagnostics.Debug.WriteLine("ReopenLastShownMenus: reopened at last position");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ReopenLastShownMenus tick ERROR: {ex.Message}");
                    }
                };
                t.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReopenLastShownMenus ERROR: {ex.Message}");
            }
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

            // Keep the indicators in sync with current config (cheap; safe if unchanged)
            SyncHotAreaIndicators(hotArea);

            // Calculate if cursor is in hot area
            bool isInHotArea = IsInHotArea(cursor, screen, hotArea);

            // Update continuous mouse catching if enabled
            MenuUIHelper.UpdateMouseCatch();

            // Show when cursor is in hot area
            if (isInHotArea)
            {
                if (!flyoutMenu.Visible)
                {
                    ShowMenus(cursor, screen, GetMenuYPosition(screen, hotArea), hotArea);
                }

                // Don't check bounds - we're in the hot area, keep menus open
                return;
            }

            // Hide menus if visible and cursor moves away from them
            // BUT only if cursor is NOT in the hot area
            // Combine bounds of any visible surface (flyout / app menu / pinned bar)
            if (flyoutMenu.Visible || appMenu.Visible || (pinnedBar != null && pinnedBar.Visible))
            {
                Rectangle combinedBounds = Rectangle.Empty;

                void AddBounds(Rectangle r)
                {
                    combinedBounds = combinedBounds.IsEmpty ? r : Rectangle.Union(combinedBounds, r);
                }

                if (flyoutMenu.Visible) AddBounds(flyoutMenu.Bounds);
                if (appMenu.Visible) AddBounds(appMenu.Bounds);
                if (pinnedBar?.Visible == true) AddBounds(pinnedBar.Bounds);

                var padded = Rectangle.Inflate(combinedBounds, 8, 8);

                // Only close if:
                // 1. Cursor is outside padded bounds
                // 2. AND cursor is NOT in hot area (already checked above)
                if (!padded.Contains(cursor))
                {
                    MenuUIHelper.DisableMouseCatch();
                    flyoutMenu.Close();
                    appMenu.Close();
                    DestroyPinnedBar();
                }
            }
        }

        /// <summary>
        /// Determines if cursor is in the configured hot area
        /// </summary>
        private static bool IsInHotArea(Point cursor, Screen screen, HotAreaConfig hotArea)
        {
            if (!IsMonitorAllowed(screen, hotArea))
                return false;

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
        /// Returns true if the given screen is allowed by the Monitors config
        /// (null/empty list => all monitors allowed).
        /// </summary>
        internal static bool IsMonitorAllowed(Screen screen, HotAreaConfig hotArea)
        {
            var monitors = hotArea.Monitors;
            if (monitors == null || monitors.Count == 0)
                return true;

            var all = Screen.AllScreens;
            int index1Based = Array.IndexOf(all, screen) + 1;
            return index1Based > 0 && monitors.Contains(index1Based);
        }

        /// <summary>
        /// Ensures there is exactly one HotAreaIndicator per allowed monitor and
        /// updates each with the current config.
        /// </summary>
        private void SyncHotAreaIndicators(HotAreaConfig hotArea)
        {
            var allowedScreens = Screen.AllScreens.Where(s => IsMonitorAllowed(s, hotArea)).ToList();

            // Grow to match count
            while (hotAreaIndicators.Count < allowedScreens.Count)
            {
                hotAreaIndicators.Add(new HotAreaIndicator());
            }

            // Shrink if there are now fewer allowed screens
            while (hotAreaIndicators.Count > allowedScreens.Count)
            {
                int last = hotAreaIndicators.Count - 1;
                hotAreaIndicators[last].Dispose();
                hotAreaIndicators.RemoveAt(last);
            }

            for (int i = 0; i < allowedScreens.Count; i++)
            {
                hotAreaIndicators[i].ApplyConfig(hotArea, allowedScreens[i]);
            }
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

                if (lowerMessage == "quit" || lowerMessage == "exit" || lowerMessage == "stop")
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
                        ShowMenus(cursor, screen, cursor.Y, hotArea);
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

            try
            {
                VirtualDesktop.CurrentChanged -= OnVirtualDesktopCurrentChanged;
            }
            catch { }

            NotifyIcon.Visible = false;
            NotifyIcon.Dispose();
            TrayMenu.Dispose();
            flyoutMenu.Dispose();
            appMenu.Dispose();  // Clean up app menu

            foreach (var ind in hotAreaIndicators) ind.Dispose();
            hotAreaIndicators.Clear();

            DestroyPinnedBar();

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

                PollTimer?.Stop();
                PollTimer?.Dispose();
                NotifyIcon?.Dispose();
                TrayMenu?.Dispose();
                flyoutMenu?.Dispose();
appMenu?.Dispose();  // Clean up app menu
                foreach (var ind in hotAreaIndicators) ind.Dispose();
                hotAreaIndicators.Clear();
                DestroyPinnedBar();
}

   base.Dispose(disposing);
        }

        /// <summary>
        /// Prevents a ContextMenuStrip from appearing in the Windows taskbar
        /// by setting WS_EX_TOOLWINDOW and WS_EX_NOACTIVATE extended window styles
        /// </summary>
        private static void PreventTaskbarAppearance(ContextMenuStrip menu)
        {
            try
            {
                return;
                // Get the window handle using reflection
                var handleProperty = typeof(ToolStripDropDown).GetProperty("Handle",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

                if (handleProperty != null)
                {
                    var handle = (IntPtr?)handleProperty.GetValue(menu);
                    if (handle.HasValue && handle.Value != IntPtr.Zero)
                    {
                        // Get current extended window styles
                        int exStyle = GetWindowLong(handle.Value, GWL_EXSTYLE);

                        // Add WS_EX_TOOLWINDOW (prevents taskbar button) and WS_EX_NOACTIVATE (prevents activation)
                        exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;

                        // Set the new extended window styles
                        SetWindowLong(handle.Value, GWL_EXSTYLE, exStyle);

                        System.Diagnostics.Debug.WriteLine($"PreventTaskbarAppearance: Set window styles for handle 0x{handle.Value:X}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PreventTaskbarAppearance: Error setting window styles: {ex.Message}");
            }
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
