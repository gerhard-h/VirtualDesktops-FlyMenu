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
        private readonly ContextMenuStrip flyoutMenu;  // Make it readonly, remove null!
        private System.Windows.Forms.Timer pollTimer = null!;
        private MessageWindow? messageWindow;

        public static VirtualDesktop?[] DesktopHistory = new VirtualDesktop?[2];

        public NotifyIcon NotifyIcon => notifyIcon;
        public ContextMenuStrip TrayMenu => trayMenu;
        // Remove: public ContextMenuStrip DummyMenu { get => flyoutMenu; set => flyoutMenu = value; }
        public ContextMenuStrip FlyoutMenu => flyoutMenu;  // Optional: expose if needed
        public System.Windows.Forms.Timer PollTimer { get => pollTimer; set => pollTimer = value; }

        public TrayApplicationContext()
        {
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
            flyoutMenu = new ContextMenuStrip();  // Changed from DummyMenu
            flyoutMenu.Closed += (s, e) => { /* no-op - poller controls show/hide */ };

            // Subscribe to VirtualDesktop changes
            VirtualDesktop.CurrentChanged += OnVirtualDesktopCurrentChanged;

            CreatePollTimer();
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
                MenuUIHelper.ShowMenuCenteredUnderCursor(flyoutMenu, cursor, screen, cursor.Y, hotArea.Edge, hotArea.CatchMouse, hotArea.triggerHeight);
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

            // Update continuous mouse catching if enabled
            MenuUIHelper.UpdateMouseCatch();

            // Calculate bounds based on configured edge and percentages
            bool isInHotArea = IsInHotArea(cursor, screen, hotArea);

            // Show when cursor is in hot area
            if (isInHotArea)
            {
                if (!flyoutMenu.Visible)
                {
                    PopulateMenuFromConfig();
                    MenuUIHelper.ShowMenuCenteredUnderCursor(flyoutMenu, cursor, screen, GetMenuYPosition(screen, hotArea), hotArea.Edge, hotArea.CatchMouse, hotArea.triggerHeight);
                }

                return;
            }

            // Hide menu if visible and cursor moves away from it
            if (flyoutMenu.Visible)
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
                // Example: Parse message and execute menu action
                // The message must match the menu lable

                //MessageBox.Show($"Received message: {message}", "FlyMenu - WM_COPYDATA", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // Example usage of ExecuteMenuAction:
                // Create a MenuItemConfig based on the received message
                var config = ParseMessageToConfig(message);
                if (config != null)
                {
                    MenuActionHandler.ExecuteMenuAction(config);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling message: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses a received message string into a MenuItemConfig
        /// </summary>
        private static MenuItemConfig? ParseMessageToConfig(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            message = message.Trim();

            var configs = ConfigLoader.LoadMenuConfigs();
            
            foreach (var config in configs)
            {
                if (config.Label == message)
                {
                    return config;
                }
            }

            // Handle action types directly
            var lowerMessage = message.ToLowerInvariant();
            if (lowerMessage is "switch left" or "switch right" or "switch before")
            {
                return new MenuItemConfig
                {
                    Type = lowerMessage
                };
            }

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
                DesktopHistory[0] = args.OldDesktop;
                DesktopHistory[1] = args.NewDesktop;
                var name = args.NewDesktop?.Name ?? "Unknown";
                //notifyIcon.Text = $"FlyMenu - Current Desktop: {name}";
                //notifyIcon.ShowBalloonTip(1000, "Desktop Changed", $"Switched to desktop: {name}", ToolTipIcon.Info);
            }
            catch (Exception)
            {
                // Handle or log error if needed
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
            }

            base.Dispose(disposing);
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
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_COPYDATA)
            {
                try
                {
                    var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(m.LParam);
                    if (cds.cbData > 0 && cds.lpData != IntPtr.Zero)
                    {
                        string message = Marshal.PtrToStringUTF8(cds.lpData, cds.cbData - 1) ?? string.Empty;
                        context.HandleReceivedMessage(message);
                        m.Result = (IntPtr)1; // Return 1 to indicate success
                        return;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in WndProc: {ex.Message}");
                }
            }

            base.WndProc(ref m);
        }
    }
}
