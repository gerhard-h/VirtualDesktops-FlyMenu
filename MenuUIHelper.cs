using System.Drawing;
using System.Windows.Forms;

namespace FlyMenu
{
    /// <summary>
    /// Handles menu UI operations like icon loading and positioning
    /// </summary>
    internal static class MenuUIHelper
    {
        private static readonly string IconsFolder = Path.Combine(AppContext.BaseDirectory, "icons");

        // Mouse catching state
        private static Rectangle lastMenuBounds = Rectangle.Empty;
        private static bool shouldCatchMouse = false;
        private static int catchHeight = 10;
        private static string? catchEdge = "top";

        /// <summary>
        /// Loads an icon from the specified path and assigns it to a menu item.
        /// If the path has no directory, searches in the icons subfolder first.
        /// </summary>
        public static void LoadIconForMenuItem(ToolStripMenuItem menuItem, string? iconPath)
        {
            if (string.IsNullOrWhiteSpace(iconPath))
                return;

            try
            {
                string resolvedPath = ResolveIconPath(iconPath);

                if (File.Exists(resolvedPath))
                {
                    menuItem.Image = new Icon(resolvedPath).ToBitmap();
                }
            }
            catch
            {
                // Ignore icon loading errors and continue without icon
            }
        }

        /// <summary>
        /// Resolves icon path by checking:
        /// 1. If path has a directory component, use as-is
        /// 2. If path is just a filename, search in icons subfolder first
        /// 3. Then fall back to root directory
        /// </summary>
        private static string ResolveIconPath(string iconPath)
        {
            // If path contains directory separators, use as-is (absolute or relative path)
            if (iconPath.Contains(Path.DirectorySeparatorChar) || iconPath.Contains(Path.AltDirectorySeparatorChar))
            {
                return iconPath;
            }

            // Try icons subfolder first
            string iconsSubfolderPath = Path.Combine(IconsFolder, iconPath);
            if (File.Exists(iconsSubfolderPath))
            {
                return iconsSubfolderPath;
            }

            // Fall back to root directory (for backward compatibility)
            return iconPath;
        }

        /// <summary>
        /// Disposes all images from menu items to free memory
        /// </summary>
        public static void DisposeMenuImages(ContextMenuStrip menu)
        {
            try
            {
                foreach (ToolStripItem item in menu.Items)
                {
                    if (item is ToolStripMenuItem menuItem && menuItem.Image != null)
                    {
                        menuItem.Image?.Dispose();
                        menuItem.Image = null;
                    }
                }
            }
            catch
            {
                // Ignore disposal errors
            }
        }

        /// <summary>
        /// Positions a menu under the cursor, centered horizontally.
        /// Only repositions cursor for top/bottom edges.
        /// </summary>
        public static void ShowMenuCenteredUnderCursor(ContextMenuStrip menu, Point cursor, Screen screen, int yPosition, string? edge = "top", bool catchMouse = false, int catchHeightPixels = 10)
        {
            // Compute preferred size to determine width
            var preferred = menu.GetPreferredSize(Size.Empty);
            int menuWidth = Math.Max(100, preferred.Width);

            // Center horizontally under cursor
            int x = cursor.X - (menuWidth / 2);

            // Clamp to screen working area
            x = Math.Max(screen.WorkingArea.Left, Math.Min(x, screen.WorkingArea.Right - menuWidth));

            int y = yPosition;

            // Show at screen coordinates (top-left of menu)
            menu.Show(new Point(x, y));

            // Store menu bounds, catching settings for continuous repositioning
            lastMenuBounds = menu.Bounds;
            shouldCatchMouse = catchMouse;
            catchHeight = catchHeightPixels;
            catchEdge = edge;

            // Move cursor only for top/bottom edges
            if (edge?.ToLowerInvariant() is "top" or "bottom")
            {
                try
                {
                    var newX = Math.Max(screen.WorkingArea.Left, Math.Min(cursor.X, screen.WorkingArea.Right - 1));
                    var newY = edge?.ToLowerInvariant() == "top"
                        ? Math.Max(screen.WorkingArea.Top, Math.Min(cursor.Y + 5, screen.WorkingArea.Bottom - 1))
                        : Math.Max(screen.WorkingArea.Top, Math.Min(cursor.Y - 5, screen.WorkingArea.Bottom - 1));
                    Cursor.Position = new Point(newX, newY);
                }
                catch
                {
                    // Ignore any failures to set the cursor
                }
            }
        }

        /// <summary>
        /// Updates mouse position to keep the first menu item focused (continuous catching).
        /// Called from the timer tick to maintain mouse inside menu.
        /// Behavior depends on the configured edge and catching parameters.
        /// </summary>
        public static void UpdateMouseCatch()
        {
            if (!shouldCatchMouse || lastMenuBounds.IsEmpty)
                return;

            try
            {
                var currentCursor = Cursor.Position;

                // Create catch zone based on edge and configured parameters
                Rectangle catchZone = GetCatchZone(lastMenuBounds, catchEdge);

                // If cursor is outside catch zone, reposition it
                if (catchZone.Contains(currentCursor))
                {
                    Point newCursorPosition = GetCatchPosition(lastMenuBounds, catchEdge, currentCursor);
                    Cursor.Position = newCursorPosition;
                }
            }
            catch
            {
                // Ignore any failures to set cursor
            }
        }

        /// <summary>
        /// Calculates the catch zone (area where cursor gets repositioned) based on edge
        /// </summary>
        private static Rectangle GetCatchZone(Rectangle menuBounds, string? edge)
        {
            string edgeValue = edge?.ToLowerInvariant() ?? "top";

            return edgeValue switch
            {
                "top" => new Rectangle(
                    menuBounds.Left,
                    menuBounds.Top,
                    menuBounds.Width,
                    catchHeight),

                "bottom" => new Rectangle(
                 menuBounds.Left,
                 menuBounds.Bottom,
                 menuBounds.Width,
                 catchHeight),

                "left" => new Rectangle(
                menuBounds.Left,
                menuBounds.Top,
                catchHeight,
                menuBounds.Height),

                "right" => new Rectangle(
            menuBounds.Right - catchHeight,
            menuBounds.Top,
            catchHeight,
         menuBounds.Height),

                _ => menuBounds
            };
        }

        /// <summary>
        /// Calculates the new cursor position when it moves outside the catch zone
        /// </summary>
        private static Point GetCatchPosition(Rectangle menuBounds, string? edge, Point currentCursor)
        {
            string edgeValue = edge?.ToLowerInvariant() ?? "top";

            return edgeValue switch
            {
                "top" => new Point(
                currentCursor.X,
                catchHeight),

                "bottom" => new Point(
         currentCursor.X,
           menuBounds.Bottom - catchHeight),

                "left" => new Point(
            catchHeight,
              currentCursor.Y),

                "right" => new Point(
             menuBounds.Right - catchHeight,
            currentCursor.Y),

                _ => currentCursor
            };
        }

        /// <summary>
        /// Disables mouse catching when menu closes
        /// </summary>
        public static void DisableMouseCatch()
        {
            shouldCatchMouse = false;
            lastMenuBounds = Rectangle.Empty;
        }
    }
}
