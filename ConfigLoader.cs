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

        private static FlyMenuConfig? cachedConfig = null;
        private static DateTime lastConfigLoad = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromSeconds(3600);
        private static int cacheHitCount = 0;

        /// <summary>
        /// Loads the complete FlyMenu configuration from FlyMenu.config file
        /// </summary>
        public static FlyMenuConfig LoadConfig()
        {
            // Use cached config if it's less than 1 second old
            if (cachedConfig != null && (DateTime.Now - lastConfigLoad) < CacheExpiration)
            {
                cacheHitCount++;
                // Only log every 10th cache hit to reduce spam
                if (cacheHitCount % 10 == 0)
                {
                    //System.Diagnostics.Debug.WriteLine($"ConfigLoader: Using cached config (hit #{cacheHitCount})");
                }
                return cachedConfig;
            }

            if (cacheHitCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"ConfigLoader: Cache expired after {cacheHitCount} hits");
                cacheHitCount = 0;
            }

            System.Diagnostics.Debug.WriteLine("ConfigLoader: Loading fresh config");

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
                System.Diagnostics.Debug.WriteLine("ConfigLoader: Expanding desktop list...");
                ExpandDesktopList(config.MenuItems);
                System.Diagnostics.Debug.WriteLine($"ConfigLoader: After expansion, menu has {config.MenuItems.Count} items");

                // Cache the expanded config
                cachedConfig = config;
                lastConfigLoad = DateTime.Now;

                return config;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ConfigLoader: Error loading config: {ex.Message}");
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
        /// Gets whether to show the app menu
        /// </summary>
        public static bool GetShowAppMenu()
        {
            var config = LoadConfig();
            return config.ShowAppMenu;
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
                ShowAppMenu = false,
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
                    System.Diagnostics.Debug.WriteLine($"ExpandDesktopList: Found DESKTOP LIST placeholder at index {index}");
                    var desktops = VirtualDesktop.GetDesktops();
                    var current = VirtualDesktop.Current;
                    System.Diagnostics.Debug.WriteLine($"ExpandDesktopList: Found {desktops.Length} desktops, current = {current?.Id}");

                    // Remove the placeholder
                    items.RemoveAt(index);
                    System.Diagnostics.Debug.WriteLine("ExpandDesktopList: Removed placeholder");

                    // Insert an entry per desktop with a switch type
                    for (int i = 0; i < desktops.Length; i++)
                    {
                        var d = desktops[i];
                        var label = d.Name;
                        if (string.IsNullOrWhiteSpace(label))
                            label = $"Desktop {i + 1}";

                        System.Diagnostics.Debug.WriteLine($"ExpandDesktopList: Adding desktop {i + 1}: '{label}' (ID: {d.Id})");

                        items.Insert(index + i, new MenuItemConfig
                        {
                            Label = label,
                            Type = "switch to",
                            Parameter = d.Id.ToString()
                        });
                    }

                    System.Diagnostics.Debug.WriteLine($"ExpandDesktopList: Successfully expanded {desktops.Length} desktop entries");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ExpandDesktopList ERROR: {ex.GetType().Name}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                    // if virtual desktop enumeration fails, ignore and keep original items
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ExpandDesktopList: No DESKTOP LIST placeholder found");
            }
        }

        /// <summary>
        /// Clears the cached configuration, forcing a reload on next access
        /// </summary>
        public static void ClearCache()
        {
            System.Diagnostics.Debug.WriteLine("ConfigLoader: Clearing cache");
            cachedConfig = null;
            lastConfigLoad = DateTime.MinValue;
            cacheHitCount = 0;
        }
    }
}
