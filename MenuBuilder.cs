using System.Windows.Forms;

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
        public static void PopulateMenu(ContextMenuStrip menu, List<MenuItemConfig> configs, Action onExit)
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

                var menuItem = new ToolStripMenuItem(label);

                // Load and set icon if specified
                MenuUIHelper.LoadIconForMenuItem(menuItem, cfg.Icon);

                // Bind action to menu item
                MenuActionHandler.BindActionToMenuItem(menuItem, cfg, onExit);

                // Add to menu
                menu.Items.Add(menuItem);
            }
        }
    }
}
