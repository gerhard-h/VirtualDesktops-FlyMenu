using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

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

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const uint GW_OWNER = 4;
        private const int SW_RESTORE = 9;
        private const uint WM_GETICON = 0x007F;
        private const uint ICON_SMALL = 0;
        private const uint ICON_BIG = 1;
        private const uint ICON_SMALL2 = 2;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private class WindowInfo
        {
     public IntPtr Handle { get; set; }
            public string Title { get; set; } = string.Empty;
    public Icon? Icon { get; set; }
            public uint ProcessId { get; set; }
        }

        /// <summary>
        /// Populates the menu with running applications
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

            // Enumerate all windows
  var windows = new List<WindowInfo>();
       EnumWindows((hWnd, lParam) =>
            {
                if (IsMainWindow(hWnd))
           {
  var window = GetWindowInfo(hWnd);
       if (window != null && !string.IsNullOrWhiteSpace(window.Title))
            {
                 windows.Add(window);
          }
    }
                return true;
   }, IntPtr.Zero);

            // Sort windows by title
       windows = windows.OrderBy(w => w.Title).ToList();

            // Create menu items
        foreach (var window in windows)
      {
  var menuItem = new ToolStripMenuItem(window.Title);

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

                // Add click handler
          menuItem.Click += (s, e) =>
         {
    if (s is ToolStripMenuItem item && item.Tag is IntPtr handle)
        {
      ActivateWindow(handle);
         }
    };

           menu.Items.Add(menuItem);
  }

    System.Diagnostics.Debug.WriteLine($"AppMenuBuilder: Populated menu with {windows.Count} applications");
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

            return true;
        }

        /// <summary>
     /// Gets window information including title and icon
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
     ProcessId = processId
       };
            }
   catch
            {
           return null;
          }
        }

        /// <summary>
      /// Activates and brings a window to the foreground
        /// </summary>
        private static void ActivateWindow(IntPtr hWnd)
  {
    try
            {
    System.Diagnostics.Debug.WriteLine($"AppMenuBuilder: Activating window 0x{hWnd:X}");

       // Restore if minimized
     ShowWindow(hWnd, SW_RESTORE);

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
