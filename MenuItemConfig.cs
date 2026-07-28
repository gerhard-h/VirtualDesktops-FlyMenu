using System.Text.Json.Serialization;

namespace FlyMenu
{
    /// <summary>
    /// Configuration model for a single menu item loaded from config.json
    /// </summary>
    public class MenuItemConfig
    {
        [JsonPropertyName("label")]
   public string? Label { get; set; }

        [JsonPropertyName("type")]
  public string? Type { get; set; }

        [JsonPropertyName("parameter")]
      public string? Parameter { get; set; }

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        /// <summary>
        /// Optional icon resource index inside <see cref="Icon"/> when it points
        /// at a multi-icon container (.exe / .dll). Ignored for .ico files.
        /// </summary>
        [JsonPropertyName("iconIndex")]
        public int IconIndex { get; set; } = 0;

        /// <summary>
        /// When true, the menu is reopened at its previous position after the
        /// action executes (so the user can click again without re-triggering
        /// the hot area). Default: false.
        /// Typical use case: "Next Desktop" / "Prev Desktop" cycling.
        /// </summary>
        [JsonPropertyName("keepOpen")]
        public bool KeepOpen { get; set; } = false;
    }
}
