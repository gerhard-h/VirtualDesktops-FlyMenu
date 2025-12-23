using System.Text.Json;
using System.Windows.Forms;
using WindowsDesktop;

namespace FlyMenu
{
    /// <summary>
    /// Handles loading and parsing the FlyMenu.config file
    /// </summary>
    internal static class ConfigLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new() 
      { 
   PropertyNameCaseInsensitive = true,
      ReadCommentHandling = JsonCommentHandling.Skip,
         AllowTrailingCommas = true
        };

        /// <summary>
        /// Loads the complete FlyMenu configuration from FlyMenu.config file
        /// </summary>
        public static FlyMenuConfig LoadConfig()
        {
            try
            {
                var configPath = Path.Combine(AppContext.BaseDirectory, "FlyMenu.config");
                if (!File.Exists(configPath))
                {
                    MessageBox.Show($"Config file not found at: {configPath}", "Config Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return GetDefaultConfig();
                }

                // Read file with FileShare.Read to allow file to be modified while app is running
                string json;
                using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream))
                {
                    json = reader.ReadToEnd();
                }

                FlyMenuConfig? config;
                try
                {
                    config = JsonSerializer.Deserialize<FlyMenuConfig>(json, JsonOptions);
                }
                catch (JsonException jsonEx)
                {
                    MessageBox.Show($"JSON parsing error in FlyMenu.config:\n{jsonEx.Message}\n\nLine: {jsonEx.LineNumber}, Byte Position: {jsonEx.BytePositionInLine}", "JSON Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return GetDefaultConfig();
                }

                if (config == null)
                {
                    return GetDefaultConfig();
                }

                // Apply defaults if sections are missing
                config.HotArea ??= new HotAreaConfig();
                config.Styling ??= new StylingConfig();
                config.MenuItems ??= new List<MenuItemConfig>();

                // Validate hot area settings
                ValidateHotArea(config.HotArea);

                // Expand "DESKTOP LIST" placeholder with actual desktops
                ExpandDesktopList(config.MenuItems);

                return config;
            }
            catch
            {
                return GetDefaultConfig();
            }
        }

        /// <summary>
        /// Loads menu items from config (for backward compatibility)
        /// </summary>
        public static List<MenuItemConfig> LoadMenuConfigs()
        {
            var config = LoadConfig();
            return config.MenuItems ?? new List<MenuItemConfig>();
        }

        /// <summary>
        /// Gets the hot area configuration
        /// </summary>
        public static HotAreaConfig GetHotAreaConfig()
        {
            var config = LoadConfig();
            return config.HotArea ?? new HotAreaConfig();
        }

        /// <summary>
        /// Gets the styling configuration
        /// </summary>
        public static StylingConfig GetStylingConfig()
        {
            var config = LoadConfig();
            return config.Styling ?? new StylingConfig();
        }

        /// <summary>
        /// Returns a default configuration with standard settings
        /// </summary>
        private static FlyMenuConfig GetDefaultConfig()
        {
            return new FlyMenuConfig
            {
                HotArea = new HotAreaConfig
                {
                    Edge = "top",
                    StartPercentage = 20,
                    EndPercentage = 60
                },
                Styling = new StylingConfig
                {
                    FontName = "Segoe UI",
                    FontSize = 9
                },
                MenuItems = new List<MenuItemConfig>()
            };
        }

        /// <summary>
        /// Validates and corrects hot area settings
        /// </summary>
        private static void ValidateHotArea(HotAreaConfig hotArea)
        {
            // Validate edge
            var validEdges = new[] { "top", "bottom", "left", "right" };
            if (string.IsNullOrWhiteSpace(hotArea.Edge) || 
                !validEdges.Contains(hotArea.Edge.ToLowerInvariant()))
            {
                hotArea.Edge = "top";
            }
            else
            {
                hotArea.Edge = hotArea.Edge.ToLowerInvariant();
            }

            // Validate percentages (0-100)
            hotArea.StartPercentage = Math.Max(0, Math.Min(100, hotArea.StartPercentage));
            hotArea.EndPercentage = Math.Max(0, Math.Min(100, hotArea.EndPercentage));

            // Ensure start <= end
            if (hotArea.StartPercentage > hotArea.EndPercentage)
            {
                (hotArea.StartPercentage, hotArea.EndPercentage) = (hotArea.EndPercentage, hotArea.StartPercentage);
            }
        }

        /// <summary>
        /// Expands "DESKTOP LIST" placeholder with actual desktop entries
        /// </summary>
        private static void ExpandDesktopList(List<MenuItemConfig> items)
        {
            int index = items.FindIndex(i => string.Equals(i.Label, "DESKTOP LIST", StringComparison.OrdinalIgnoreCase));
            if (index != -1)
            {
                try
                {
                    var desktops = VirtualDesktop.GetDesktops();
                    var current = VirtualDesktop.Current;

                    // Remove the placeholder
                    items.RemoveAt(index);

                    // Insert an entry per desktop with a switch type
                    for (int i = 0; i < desktops.Length; i++)
                    {
                        var d = desktops[i];
                        var label = d.Name;
                        if (string.IsNullOrWhiteSpace(label))
                            label = $"Desktop {i + 1}";

                        items.Insert(index + i, new MenuItemConfig
                        {
                            Label = label,
                            Type = "switch to",
                            Parameter = d.Id.ToString()
                        });
                    }
                }
                catch
                {
                    // if virtual desktop enumeration fails, ignore and keep original items
                }
            }
        }
    }
}
