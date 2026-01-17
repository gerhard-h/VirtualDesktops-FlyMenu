using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using WindowsDesktop;  // Add this for VirtualDesktop support

namespace FlyMenu
{
    /// <summary>
    /// Builds a menu of running applications with icons
    /// </summary>
    internal static class AppMenuBuilder
    {
        // P/Invoke declarations for Windows API
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint GW_OWNER = 4;
        private const uint GW_HWNDNEXT = 2;  // For Z-order enumeration
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_SHOWMINIMIZED = 2;
        private const uint WM_GETICON = 0x007F;
        private const uint ICON_SMALL = 0;
        private const uint ICON_BIG = 1;
        private const uint ICON_SMALL2 = 2;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        // Sort order tracking - key is desktop ID, value is sort mode (true = alphabetical, false = last used)
        private static readonly Dictionary<Guid, bool> desktopSortOrder = new Dictionary<Guid, bool>();

        private class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; } = string.Empty;
            public Icon? Icon { get; set; }
            public uint ProcessId { get; set; }
            public Guid? DesktopId { get; set; }  // Track which desktop the window is on
            public int ZOrder { get; set; }  // Z-order position (0 = topmost, higher = lower)
        }

        /// <summary>
        /// Populates the menu with running applications, grouped by virtual desktop
        /// </summary>
        public static void PopulateAppMenu(ContextMenuStrip menu)
        {
            // Clear existing items
            menu.Items.Clear();

            // Get styling configuration
            var styling = ConfigLoader.GetStylingConfig();

            // Apply font to menu if configured
            if (styling != null && !string.IsNullOrWhiteSpace(styling.FontName))
            {
                try
                {
                    menu.Font = new Font(styling.FontName, styling.FontSize);
                }
                catch
                {
                    // If font fails to load, keep default font
                }
            }

            // Enumerate all windows with Z-order
            var windows = new List<WindowInfo>();
            int zOrder = 0;
            EnumWindows((hWnd, lParam) =>
               {
                   if (IsMainWindow(hWnd))
                   {
                       var window = GetWindowInfo(hWnd);
                       if (window != null && !string.IsNullOrWhiteSpace(window.Title))
                       {
                           window.ZOrder = zOrder++;
                           windows.Add(window);
                       }
                   }
                   return true;
               }, IntPtr.Zero);

            // Get all desktops for ordering
            var allDesktops = VirtualDesktop.GetDesktops();
            var currentDesktop = VirtualDesktop.Current;

            // Group windows by desktop
            var windowsByDesktop = windows
            .GroupBy(w => w.DesktopId)
              .OrderBy(g =>
          {
              // Put current desktop first
              if (g.Key == currentDesktop?.Id) return 0;

              // Then order by desktop index
              var desktopIndex = Array.FindIndex(allDesktops, d => d.Id == g.Key);
              return desktopIndex >= 0 ? desktopIndex + 1 : int.MaxValue;
          })
              .ToList();

            // Create menu items grouped by desktop
            bool firstGroup = true;
            foreach (var desktopGroup in windowsByDesktop)
            {
                // Add separator between desktop groups (except before first group)
                if (!firstGroup)
                {
                    menu.Items.Add(new ToolStripSeparator());
                }
                firstGroup = false;

                // Find desktop name
                string desktopName = "Unknown Desktop";
                var desktop = allDesktops.FirstOrDefault(d => d.Id == desktopGroup.Key);
                Guid? desktopId = desktopGroup.Key;

                if (desktop != null)
                {
                    desktopName = string.IsNullOrWhiteSpace(desktop.Name)
               ? $"Desktop {Array.IndexOf(allDesktops, desktop) + 1}"
                    : desktop.Name;

                    // Mark current desktop
                    if (desktop.Id == currentDesktop?.Id)
                    {
                        desktopName = $"{desktopName} ●";  // Current desktop indicator
                    }
                }

                // Get or initialize sort order for this desktop (default: alphabetical = true)
                if (desktopId.HasValue && !desktopSortOrder.ContainsKey(desktopId.Value))
                {
                    desktopSortOrder[desktopId.Value] = true;  // Default to alphabetical
                }

                bool isAlphabetical = desktopId.HasValue ? desktopSortOrder[desktopId.Value] : true;
                string sortIndicator = isAlphabetical ? "▲ " : "◆ ";

                // Add desktop header (clickable to toggle sort)
                var header = new ToolStripMenuItem(sortIndicator + desktopName)
                {
                    Font = new Font(menu.Font, FontStyle.Bold),
                    Tag = desktopId  // Store desktop ID for sorting toggle
                };

                // Make header clickable to toggle sort order
                header.Click += (s, e) =>
                   {
                       if (s is ToolStripMenuItem item && item.Tag is Guid deskId)
                       {
                           // Toggle sort order
                           desktopSortOrder[deskId] = !desktopSortOrder[deskId];
                           System.Diagnostics.Debug.WriteLine($"AppMenuBuilder: Toggled sort for desktop {deskId} to {(desktopSortOrder[deskId] ? "Alphabetical" : "Last Used")}");

                           // Refresh the menu in place without closing it
                           // Store the current menu location
                           var currentLocation = menu.Location;
                           var wasVisible = menu.Visible;

                           if (wasVisible)
                           {
                               // Suspend layout to prevent flickering
                               menu.SuspendLayout();

                               // Repopulate the menu
                               PopulateAppMenu(menu);

                               // Resume layout
                               menu.ResumeLayout();

                               // Force the menu to refresh its display
                               menu.Refresh();

                               System.Diagnostics.Debug.WriteLine($"AppMenuBuilder: Menu refreshed at location {currentLocation}");
                           }
                       }
                   };

                menu.Items.Add(header);

                // Sort windows within this desktop based on current sort order
                List<WindowInfo> sortedWindows;
                if (isAlphabetical)
                {
                    sortedWindows = desktopGroup.OrderBy(w => w.Title).ToList();
                }
                else
                {
                    // Sort by Z-order (last used = lower Z-order number)
                    sortedWindows = desktopGroup.OrderBy(w => w.ZOrder).ToList();
                }

                // Add windows for this desktop
                foreach (var window in sortedWindows)
                {
                    var menuItem = new ToolStripMenuItem("  " + window.Title);  // Indent with spaces

                    // Set icon if available
                    if (window.Icon != null)
                    {
                        try
                        {
                            menuItem.Image = window.Icon.ToBitmap();
                        }
                        catch
                        {
                            // Ignore icon errors
                        }
                    }

                    // Store window handle in Tag
                    menuItem.Tag = window.Handle;

                    // Add click handler - close menus first, then activate window
                    menuItem.Click += (s, e) =>
               {
                   // Close all menus first
                   MenuActionHandler.CloseMenus();

                   // Then activate the window
                   if (s is ToolStripMenuItem item && item.Tag is IntPtr handle)
                   {
                       ActivateWindow(handle);
                   }
               };

                    menu.Items.Add(menuItem);
                }
            }

            System.Diagnostics.Debug.WriteLine($"AppMenuBuilder: Populated menu with {windows.Count} applications across {windowsByDesktop.Count} desktops");
        }

        /// <summary>
        /// Determines if a window is a main application window
        /// </summary>
        private static bool IsMainWindow(IntPtr hWnd)
        {
            // Must be visible
            if (!IsWindowVisible(hWnd))
                return false;

            // Must not be a tool window
            int exStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return false;

            // Must not have an owner
            IntPtr owner = GetWindow(hWnd, GW_OWNER);
            if (owner != IntPtr.Zero)
                return false;

            // Must have a title
            int length = GetWindowTextLength(hWnd);
            if (length == 0)
                return false;

            // Filter out FlyMenu itself and Windows system apps
            try
            {
                GetWindowThreadProcessId(hWnd, out uint processId);
                var process = Process.GetProcessById((int)processId);
                string? processName = process.ProcessName?.ToLowerInvariant();

                // Exclude FlyMenu itself
                if (processName == "flymenu")
                    return false;

                // Exclude Windows system apps (all lowercase for comparison)
                if (processName is "textinputhost" or      // Windows-Eingabeerfahrung
                "searchhost" or         // Windows Search
           "shellexperiencehost" or  // Start Menu, Action Center
        "applicationframehost" or // UWP container
              "systemsettings")      // Windows Settings (fixed: lowercase)
                {
                    return false;
                }
            }
            catch
            {
                // If we can't get process info, ignore the error and continue
            }

            return true;
        }

        /// <summary>
        /// Gets window information including title, icon, and desktop
        /// </summary>
        private static WindowInfo? GetWindowInfo(IntPtr hWnd)
        {
            try
            {
                // Get window title
                int length = GetWindowTextLength(hWnd);
                if (length == 0)
                    return null;

                var title = new StringBuilder(length + 1);
                GetWindowText(hWnd, title, title.Capacity);

                // Get process ID
                GetWindowThreadProcessId(hWnd, out uint processId);

                // Get desktop ID
                Guid? desktopId = null;
                try
                {
                    var desktop = VirtualDesktop.FromHwnd(hWnd);
                    desktopId = desktop?.Id;
                }
                catch
                {
                    // If we can't get desktop, window might be on all desktops or there's an error
                    System.Diagnostics.Debug.WriteLine($"Could not get desktop for window: {title}");
                }

                // Get window icon
                Icon? icon = null;
                try
                {
                    // Try to get icon from window
                    IntPtr iconHandle = SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero);
                    if (iconHandle == IntPtr.Zero)
                        iconHandle = SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero);
                    if (iconHandle == IntPtr.Zero)
                        iconHandle = SendMessage(hWnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);

                    if (iconHandle != IntPtr.Zero)
                    {
                        icon = Icon.FromHandle(iconHandle);
                    }
                    else
                    {
                        // Try to get icon from process
                        var process = Process.GetProcessById((int)processId);
                        if (!string.IsNullOrEmpty(process.MainModule?.FileName))
                        {
                            icon = Icon.ExtractAssociatedIcon(process.MainModule.FileName);
                        }
                    }
                }
                catch
                {
                    // Ignore icon extraction errors
                }

                return new WindowInfo
                {
                    Handle = hWnd,
                    Title = title.ToString(),
                    Icon = icon,
                    ProcessId = processId,
                    DesktopId = desktopId
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Activates and brings a window to the foreground, preserving its maximized state
        /// </summary>
        private static void ActivateWindow(IntPtr hWnd)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"AppMenuBuilder: Activating window 0x{hWnd:X}");

                // Get window placement to check current and restore state
                WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
                placement.length = Marshal.SizeOf(placement);
                GetWindowPlacement(hWnd, ref placement);

                System.Diagnostics.Debug.WriteLine($"AppMenuBuilder: Window showCmd = {placement.showCmd}");

                // Check if window is currently minimized (showCmd == SW_SHOWMINIMIZED or similar)
                if (placement.showCmd == SW_SHOWMINIMIZED || placement.showCmd == 2)
                {
                    // Window is minimized - restore it
                    // SW_RESTORE will restore to whatever state it was before (normal or maximized)
                    ShowWindow(hWnd, SW_RESTORE);
                    System.Diagnostics.Debug.WriteLine("AppMenuBuilder: Restored minimized window");
                }
                else
                {
                    // Window is not minimized - just activate it without changing state
                    // This preserves maximized state
                    ShowWindow(hWnd, SW_SHOW);
                    System.Diagnostics.Debug.WriteLine("AppMenuBuilder: Showed window (preserved state)");
                }

                // Bring to foreground
                SetForegroundWindow(hWnd);

                System.Diagnostics.Debug.WriteLine("AppMenuBuilder: Window activated successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AppMenuBuilder: Error activating window: {ex.Message}");
                MessageBox.Show($"Failed to activate window: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
