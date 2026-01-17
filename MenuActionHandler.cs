using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using WindowsDesktop;

namespace FlyMenu
{
    /// <summary>
    /// Handles binding and executing menu item actions based on configuration type
    /// </summary>
    internal static class MenuActionHandler
    {
        // Store references to menus so we can close them before actions execute
        private static ContextMenuStrip? flyoutMenu;
        private static ContextMenuStrip? appMenu;

        /// <summary>
        /// Sets the menu references so they can be closed when actions execute
        /// </summary>
        public static void SetMenuReferences(ContextMenuStrip? flyout, ContextMenuStrip? app)
        {
            flyoutMenu = flyout;
            appMenu = app;
        }

        /// <summary>
        /// Closes all menus immediately
        /// </summary>
        public static void CloseMenus()
        {
            try
            {
                flyoutMenu?.Close();
                appMenu?.Close();
            }
            catch
            {
                // Ignore errors closing menus
            }
        }

        /// <summary>
        /// Binds a click event to a menu item based on its configured type
        /// </summary>
        public static void BindActionToMenuItem(ToolStripMenuItem menuItem, MenuItemConfig config)
        {
            menuItem.Click += (s, e) =>
            {
                CloseMenus();
                ExecuteMenuAction(config);
            };
        }

        /// <summary>
        /// Executes a menu action based on its configuration without requiring a menu item
        /// </summary>
        public static void ExecuteMenuAction(MenuItemConfig config)
        {
            var type = config.Type?.ToLowerInvariant() ?? string.Empty;

            switch (type)
            {
                case "exit":
                    break;

                case "about":
                    MessageBox.Show("FlyMenu\nTray application", "About");
                    break;

                case "switch before":
                    SwitchToPreviousDesktop();
                    break;

                case "switch to":
                    SwitchToDesktop(config.Parameter);
                    break;

                case "switch right":
                    SwitchToRightDesktop();
                    break;

                case "switch left":
                    SwitchToLeftDesktop();
                    break;

                case "open":
                    MessageBox.Show("Opening the selected application...", "Action");
                    break;

                case "run":
                    RunCommand(config.Parameter);
                    break;

                case "shortcut":
                    ExecuteShortcut(config.Parameter);
                    break;

                default:
                    MessageBox.Show($"You clicked {config.Label}", "Menu Click");
                    break;
            }
        }

        private static void SwitchToPreviousDesktop()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("SwitchToPreviousDesktop: Checking desktop history...");
                if (TrayApplicationContext.DesktopHistory[0] != null)
                {
                    System.Diagnostics.Debug.WriteLine($"SwitchToPreviousDesktop: Switching to history desktop {TrayApplicationContext.DesktopHistory[0]?.Id}");
                    TrayApplicationContext.DesktopHistory[0]?.Switch();
                    System.Diagnostics.Debug.WriteLine("SwitchToPreviousDesktop: Switch completed successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("SwitchToPreviousDesktop: No history, getting all desktops...");
                    var desktops = VirtualDesktop.GetDesktops();
                    System.Diagnostics.Debug.WriteLine($"SwitchToPreviousDesktop: Found {desktops.Length} desktops");
                    desktops.LastOrDefault()?.Switch();
                    System.Diagnostics.Debug.WriteLine("SwitchToPreviousDesktop: Switched to last desktop");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SwitchToPreviousDesktop ERROR: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Failed to switch desktop: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void SwitchToDesktop(string? parameter)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"SwitchToDesktop: Parameter = '{parameter}'");
                if (string.IsNullOrEmpty(parameter) || !Guid.TryParse(parameter, out var id))
                {
                    System.Diagnostics.Debug.WriteLine("SwitchToDesktop ERROR: Invalid GUID parameter");
                    MessageBox.Show("Invalid desktop id in config parameter", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"SwitchToDesktop: Parsed GUID = {id}");

                try
                {
                    System.Diagnostics.Debug.WriteLine("SwitchToDesktop: Attempting FromId...");
                    // Preferred: use API if available
                    var target = VirtualDesktop.FromId(id);
                    System.Diagnostics.Debug.WriteLine($"SwitchToDesktop: FromId returned {(target != null ? "desktop" : "null")}");
                    target?.Switch();
                    System.Diagnostics.Debug.WriteLine("SwitchToDesktop: Switch completed successfully");
                }
                catch (Exception innerEx)
                {
                    System.Diagnostics.Debug.WriteLine($"SwitchToDesktop: FromId failed with {innerEx.GetType().Name}: {innerEx.Message}");
                    System.Diagnostics.Debug.WriteLine("SwitchToDesktop: Trying fallback enumeration...");
                    // Fallback: enumerate and match Id
                    var desktops = VirtualDesktop.GetDesktops();
                    System.Diagnostics.Debug.WriteLine($"SwitchToDesktop: Found {desktops.Length} desktops");
                    var found = desktops.FirstOrDefault(d => d.Id == id);
                    System.Diagnostics.Debug.WriteLine($"SwitchToDesktop: Match found = {found != null}");
                    found?.Switch();
                    System.Diagnostics.Debug.WriteLine("SwitchToDesktop: Fallback switch completed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SwitchToDesktop ERROR: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Failed to switch desktop: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void SwitchToRightDesktop()
        {
            try
            {
                var current = VirtualDesktop.Current;
                var right = current.GetRight();
                if (right != null)
                {
                    right.Switch();
                }
                else
                {
                    // Optionally wrap to first
                    var desktops = VirtualDesktop.GetDesktops();
                    desktops.FirstOrDefault()?.Switch();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to switch desktop: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private static void SwitchToLeftDesktop()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("SwitchToLeftDesktop: Getting current desktop...");
                var current = VirtualDesktop.Current;
                System.Diagnostics.Debug.WriteLine($"SwitchToLeftDesktop: Current desktop = {current?.Id}");

                var left = current.GetLeft();
                System.Diagnostics.Debug.WriteLine($"SwitchToLeftDesktop: Left desktop = {left?.Id}");

                if (left != null)
                {
                    System.Diagnostics.Debug.WriteLine("SwitchToLeftDesktop: Switching to left desktop...");
                    left.Switch();
                    System.Diagnostics.Debug.WriteLine("SwitchToLeftDesktop: Switch completed successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("SwitchToLeftDesktop: No left desktop, wrapping to last...");
                    // Optionally wrap to first
                    var desktops = VirtualDesktop.GetDesktops();
                    desktops.LastOrDefault()?.Switch();
                    System.Diagnostics.Debug.WriteLine("SwitchToLeftDesktop: Wrapped to last desktop");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SwitchToLeftDesktop ERROR: {ex.GetType().Name}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Failed to switch desktop: {ex.Message}\n\nType: {ex.GetType().Name}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void RunCommand(string? parameter)
        {
            try
            {
                var param = parameter?.Trim();
                if (string.IsNullOrEmpty(param))
                {
                    MessageBox.Show("No command specified in parameter", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var (file, args) = ParseCommandLine(param);
                var psi = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = args,
                    UseShellExecute = true,
                    WorkingDirectory = AppContext.BaseDirectory
                };

                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to run '{parameter}': {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static (string file, string args) ParseCommandLine(string param)
        {
            string file = param;
            string args = string.Empty;

            if (param.StartsWith("\""))
            {
                // "C:\path with spaces\app.exe" arg1 arg2
                int endQuote = param.IndexOf('"', 1);
                if (endQuote > 1)
                {
                    file = param.Substring(1, endQuote - 1);
                    args = param.Substring(endQuote + 1).Trim();
                }
            }
            else
            {
                int idx = param.IndexOf(' ');
                if (idx > 0)
                {
                    file = param.Substring(0, idx);
                    args = param.Substring(idx + 1).Trim();
                }
            }

            return (file, args);
        }

        private static void ExecuteShortcut(string? shortcutParameter)
        {
            try
            {
                shortcutParameter = shortcutParameter?.Trim();
                if (string.IsNullOrEmpty(shortcutParameter))
                {
                    MessageBox.Show("No shortcut method specified in parameter", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Check if it's a string-based shortcut format
                if (shortcutParameter.Contains('(') && shortcutParameter.Contains(')'))
                {
                    // Format: KeyPress(F3) or ModifiedKeyStroke(CONTROL MENU, ESCAPE VK_K)
                    KeyboardHelper.ExecuteShortcut(shortcutParameter);
                    return;
                }

                // Fall back to method name lookup via reflection
                var method = typeof(KeyboardHelper).GetMethod(
                        shortcutParameter,
                 BindingFlags.Public | BindingFlags.Static,
                   null,
               Type.EmptyTypes,
              null);

                if (method == null)
                {
                    MessageBox.Show($"Shortcut method '{shortcutParameter}' not found in KeyboardHelper", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                method.Invoke(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to execute shortcut: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
