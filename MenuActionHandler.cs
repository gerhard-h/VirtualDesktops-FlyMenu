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
        /// <summary>
        /// Binds a click event to a menu item based on its configured type
        /// </summary>
        public static void BindActionToMenuItem(ToolStripMenuItem menuItem, MenuItemConfig config, Action onExit)
        {
            var type = config.Type?.ToLowerInvariant() ?? string.Empty;

            switch (type)
            {
                case "exit":
                    menuItem.Click += (s, e) => onExit();
                    break;

                case "about":
                    menuItem.Click += (s, e) =>
                 MessageBox.Show("FlyMenu\nTray application", "About");
                    break;

                case "switch before":
                    menuItem.Click += (s, e) => SwitchToPreviousDesktop();
                    break;

                case "switch to":
                    menuItem.Click += (s, e) => SwitchToDesktop(config.Parameter);
                    break;

                case "switch right":
                    menuItem.Click += (s, e) => SwitchToRightDesktop();
                    break;

                case "switch left":
                    menuItem.Click += (s, e) => SwitchToLeftDesktop();
                    break;

                case "open":
                    menuItem.Click += (s, e) =>
                             MessageBox.Show("Opening the selected application...", "Action");
                    break;

                case "run":
                    menuItem.Click += (s, e) => RunCommand(config.Parameter);
                    break;

                case "shortcut":
                    menuItem.Click += (s, e) => ExecuteShortcut(config.Parameter);
                    break;

                default:
                    menuItem.Click += (s, e) =>
                   MessageBox.Show($"You clicked {config.Label}", "Menu Click");
                    break;
            }
        }

        private static void SwitchToPreviousDesktop()
        {
            try
            {
                if (TrayApplicationContext.DesktopHistory[0] != null)
                {
                    TrayApplicationContext.DesktopHistory[0]?.Switch();
                }
                else
                {
                    var desktops = VirtualDesktop.GetDesktops();
                    desktops.LastOrDefault()?.Switch();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to switch desktop: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void SwitchToDesktop(string? parameter)
        {
            try
            {
                if (string.IsNullOrEmpty(parameter) || !Guid.TryParse(parameter, out var id))
                {
                    MessageBox.Show("Invalid desktop id in config parameter", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    // Preferred: use API if available
                    var target = VirtualDesktop.FromId(id);
                    target?.Switch();
                }
                catch
                {
                    // Fallback: enumerate and match Id
                    var desktops = VirtualDesktop.GetDesktops();
                    var found = desktops.FirstOrDefault(d => d.Id == id);
                    found?.Switch();
                }
            }
            catch (Exception ex)
            {
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
                var current = VirtualDesktop.Current;
                var left = current.GetLeft();
                if (left != null)
                {
                    left.Switch();
                }
                else
                {
                    // Optionally wrap to first
                    var desktops = VirtualDesktop.GetDesktops();
                    desktops.LastOrDefault()?.Switch();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to switch desktop: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
