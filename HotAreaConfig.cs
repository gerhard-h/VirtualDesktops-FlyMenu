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

        [JsonPropertyName("menuItems")]
        public List<MenuItemConfig>? MenuItems { get; set; }
    }

    /// <summary>
    /// Configuration for the hot area (trigger zone for the menu)
    /// </summary>
    public class HotAreaConfig
    {
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
