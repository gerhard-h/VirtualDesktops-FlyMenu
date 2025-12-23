using System;
using System.Drawing;
using System.Windows.Forms;
using WindowsDesktop;
using System.IO;

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
        private ContextMenuStrip flyoutMenu = null!;
        private System.Windows.Forms.Timer pollTimer = null!;

        public static VirtualDesktop?[] DesktopHistory = new VirtualDesktop?[2];

        public NotifyIcon NotifyIcon => notifyIcon;
        public ContextMenuStrip TrayMenu => trayMenu;
        public ContextMenuStrip DummyMenu { get => flyoutMenu; set => flyoutMenu = value; }
        public System.Windows.Forms.Timer PollTimer { get => pollTimer; set => pollTimer = value; }

        public TrayApplicationContext()
        {
            Application.ApplicationExit += OnApplicationExit;

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
            DummyMenu = new ContextMenuStrip();
            DummyMenu.Closed += (s, e) => { /* no-op - poller controls show/hide */ };

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
                MenuUIHelper.ShowMenuCenteredUnderCursor(DummyMenu, cursor, screen, cursor.Y, hotArea.Edge, hotArea.CatchMouse, hotArea.CatchHeight);
            }
        }

        private void PopulateMenuFromConfig()
        {
            var configs = ConfigLoader.LoadMenuConfigs();
            MenuBuilder.PopulateMenu(DummyMenu, configs, ExitApplication);
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
                if (!DummyMenu.Visible)
                {
                    PopulateMenuFromConfig();
                    MenuUIHelper.ShowMenuCenteredUnderCursor(DummyMenu, cursor, screen, GetMenuYPosition(screen, hotArea), hotArea.Edge, hotArea.CatchMouse, hotArea.CatchHeight);
                }

                return;
            }

            // Hide menu if visible and cursor moves away from it
            if (DummyMenu.Visible)
            {
                var bounds = DummyMenu.Bounds;
                var padded = Rectangle.Inflate(bounds, 8, 8);
                if (!padded.Contains(cursor))
                {
                    MenuUIHelper.DisableMouseCatch();
                    DummyMenu.Close();
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

        private void ExitApplication()
        {
            PollTimer?.Stop();
            PollTimer?.Dispose();

            try
            {
                VirtualDesktop.CurrentChanged -= OnVirtualDesktopCurrentChanged;
            }
            catch { }

            NotifyIcon.Visible = false;
            NotifyIcon.Dispose();
            TrayMenu.Dispose();
            DummyMenu.Dispose();

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

                PollTimer?.Stop();
                PollTimer?.Dispose();
                NotifyIcon?.Dispose();
                TrayMenu?.Dispose();
                DummyMenu?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
