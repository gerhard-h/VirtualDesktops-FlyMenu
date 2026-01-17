using System.Windows.Forms;
using WindowsDesktop;

namespace FlyMenu
{
    /// <summary>
    /// Handles building menu items from configuration
    /// </summary>
    internal static class MenuBuilder
    {
        /// <summary>
        /// Builds menu items from configuration and binds their actions
        /// </summary>
        public static void PopulateMenu(ContextMenuStrip menu, List<MenuItemConfig> configs)
        {
            // Dispose old images and clear menu
            MenuUIHelper.DisposeMenuImages(menu);
            menu.Items.Clear();

            // Get styling configuration
            var styling = ConfigLoader.GetStylingConfig();
            
            // Apply font to menu if configured
            if (styling != null && !string.IsNullOrWhiteSpace(styling.FontName))
            {
                try
                {
                    menu.Font = new System.Drawing.Font(styling.FontName, styling.FontSize);
                }
                catch
                {
                    // If font fails to load, keep default font
                }
            }

            // Build menu items from config
            foreach (var cfg in configs)
            {
                var label = string.IsNullOrWhiteSpace(cfg.Label) ? "" : cfg.Label!;
                if (string.IsNullOrEmpty(label))
                    continue;

                // Replace %i placeholder with desktop number
                label = ReplacePlaceholders(label, cfg);

                var menuItem = new ToolStripMenuItem(label);

                // Load and set icon if specified
                MenuUIHelper.LoadIconForMenuItem(menuItem, cfg.Icon);

                // Bind action to menu item
                MenuActionHandler.BindActionToMenuItem(menuItem, cfg);

                // Add to menu
                menu.Items.Add(menuItem);
            }
        }

        /// <summary>
        /// Replaces placeholders in menu labels with actual values
        /// </summary>
        private static string ReplacePlaceholders(string label, MenuItemConfig config)
        {
            // Replace %i with desktop number for "switch before" actions
            if (label.Contains("%i") && config.Type?.ToLowerInvariant() == "switch before")
            {
                try
                {
                    // Get the previous desktop from history
                    var previousDesktop = TrayApplicationContext.DesktopHistory[0];
                    
                    if (previousDesktop != null)
                    {
                        // Get all desktops to find the index
                        var allDesktops = VirtualDesktop.GetDesktops();
                        var desktopIndex = Array.FindIndex(allDesktops, d => d.Id == previousDesktop.Id);
        
                        if (desktopIndex >= 0)
                        {
                            // Replace %i with 1-based desktop number
                            int desktopNumber = desktopIndex + 1;
                            label = label.Replace("%i", desktopNumber.ToString());
                        }
                        else
                        {
                            // Desktop not found in list, just remove placeholder
                            label = label.Replace("%i", "?");
                        }
                    }
                    else
                    {
                        // No previous desktop in history, remove placeholder
                        label = label.Replace("%i", "-");
                    }
                }
                catch
                {
                    // If any error occurs, just remove the placeholder
                    label = label.Replace("%i", "?");
                }
            }
   
            return label;
        }
    }
}
