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
    }
}
