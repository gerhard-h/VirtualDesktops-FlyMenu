using System.Text.Json.Serialization;

namespace FlyMenu
{
    /// <summary>
    /// Root configuration for FlyMenu application
    /// </summary>
    public class FlyMenuConfig
    {
        [JsonPropertyName("hotArea")]
        public HotAreaConfig? HotArea { get; set; }
        [JsonPropertyName("styling")]
        public StylingConfig? Styling { get; set; }

        /// <summary>Optional pinned-programs bar shown above the running-programs menu.</summary>
        [JsonPropertyName("QuickLaunchBar")]
        public QuickLaunchBarConfig? QuickLaunchBar { get; set; }

        /// <summary>Running-applications menu (formerly showAppMenu/appMenu).</summary>
        [JsonPropertyName("runningApplicationsMenu")]
        public RunningApplicationsMenuConfig? RunningApplicationsMenu { get; set; }

        /// <summary>Main flyout (desktop) menu shown at the hot area.</summary>
        [JsonPropertyName("QuickLaunchMenu")]
        public QuickLaunchMenuConfig? QuickLaunchMenu { get; set; }
    }

    /// <summary>Configuration for the flyout (main) menu.</summary>
    public class QuickLaunchMenuConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("menuItems")]
        public List<MenuItemConfig>? MenuItems { get; set; }
    }

    /// <summary>Configuration for the running-applications side menu.</summary>
    public class RunningApplicationsMenuConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Default sort order for the running programs menu.
        /// Valid values: "alpha" (alphabetical) or "lastused" (Z-order / last used).
        /// </summary>
        [JsonPropertyName("defaultAppSortOrder")]
        public string DefaultAppSortOrder { get; set; } = "lastused";
    }

    /// <summary>Pinned-programs bar configuration.</summary>
    public class QuickLaunchBarConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        /// <summary>Icon size in pixels for each pinned entry. Default 32.</summary>
        [JsonPropertyName("iconSize")]
        public int IconSize { get; set; } = 32;

        /// <summary>
        /// Background color for the bar (any #RRGGBB, #AARRGGBB, or named color).
        /// Default is a bright neutral so shell icons with alpha don't appear black.
        /// </summary>
        [JsonPropertyName("backgroundColor")]
        public string? BackgroundColor { get; set; } = "#F0F0F0";

        /// <summary>Pixels of padding inside the bar, left of the first icon.</summary>
        [JsonPropertyName("paddingLeft")]
        public int PaddingLeft { get; set; } = 4;

        /// <summary>Pixels of padding inside the bar, right of the last icon.</summary>
        [JsonPropertyName("paddingRight")]
        public int PaddingRight { get; set; } = 4;

        /// <summary>Pixels of padding above the icon row.</summary>
        [JsonPropertyName("paddingTop")]
        public int PaddingTop { get; set; } = 4;

        /// <summary>Pixels of padding below the icon row.</summary>
        [JsonPropertyName("paddingBottom")]
        public int PaddingBottom { get; set; } = 4;

        /// <summary>Horizontal spacing between adjacent icons, in pixels.</summary>
        [JsonPropertyName("paddingBetween")]
        public int PaddingBetween { get; set; } = 2;

        /// <summary>
        /// Ordered list of pinned entries. Same shape as regular <see cref="MenuItemConfig"/>
        /// entries: type/parameter drive the launch action, icon/iconIndex drive the visual.
        /// Typical: type=&quot;run&quot;, parameter=&quot;C:\...\Foo.lnk&quot; or a shell:AppsFolder path.
        /// </summary>
        [JsonPropertyName("menuItems")]
        public List<MenuItemConfig>? MenuItems { get; set; }
    }
    /// <summary>
    /// Configuration for the hot area (trigger zone for the menu)
    /// </summary>
    public class HotAreaConfig
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("edge")]
        public string? Edge { get; set; } = "top";

        [JsonPropertyName("startPercentage")]
        public int StartPercentage { get; set; } = 0;

        [JsonPropertyName("endPercentage")]
        public int EndPercentage { get; set; } = 100;

        [JsonPropertyName("catchMouse")]
        public bool CatchMouse { get; set; } = true;

        [JsonPropertyName("catchHeight")]
        public int CatchHeight { get; set; } = 10;
        [JsonPropertyName("triggerHeight")]
        public int triggerHeight { get; set; } = 5;

        /// <summary>
        /// Height in pixels of the visible hot-area indicator stripe.
        /// 0 (default) disables the indicator. Only shown when Edge == "top".
        /// </summary>
        [JsonPropertyName("indicatorHeight")]
        public int IndicatorHeight { get; set; } = 0;

        /// <summary>
        /// Color of the indicator stripe. Accepts named colors ("Red")
        /// or hex ("#RRGGBB" / "#AARRGGBB"). Default: red.
        /// </summary>
        [JsonPropertyName("indicatorColor")]
        public string? IndicatorColor { get; set; } = "#FF0000";

        /// <summary>
        /// Opacity of the indicator stripe (0.0 transparent - 1.0 opaque). Default: 0.5.
        /// </summary>
        [JsonPropertyName("indicatorOpacity")]
        public double IndicatorOpacity { get; set; } = 0.5;

        /// <summary>
        /// Corner radius in pixels applied only to the bottom-left and bottom-right
        /// corners of the indicator stripe. 0 (default) = square corners.
        /// </summary>
        [JsonPropertyName("indicatorCornerRadius")]
        public int IndicatorCornerRadius { get; set; } = 0;

        /// <summary>
        /// Restricts the hot area (and visible indicator) to the listed monitors.
        /// Values are 1-based indices into <see cref="System.Windows.Forms.Screen.AllScreens"/>.
        /// When null or empty, the hot area is active on all monitors (default).
        /// Example: [1] = primary monitor only, [1,3] = first and third monitor.
        /// </summary>
        [JsonPropertyName("monitors")]
        public List<int>? Monitors { get; set; }
    }

    /// <summary>
    /// Configuration for styling (fonts, colors, etc.)
    /// </summary>
    public class StylingConfig
    {
        [JsonPropertyName("fontName")]
        public string? FontName { get; set; } = "Segoe UI";

        [JsonPropertyName("fontSize")]
        public float FontSize { get; set; } = 9;
    }
}
