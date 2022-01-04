using System.Text.Json.Serialization;

namespace Chip8Sharp;

public class Configuration
{
    [JsonPropertyName("SHOW_KEYMAPPING")] public bool ShowKeymapping { get; set; }
    [JsonPropertyName("FILE_PATH")] public string FilePath { get; set; }
}
