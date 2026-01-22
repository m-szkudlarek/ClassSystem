using System.Text.Json.Serialization;

namespace ClassSystem.Configuration;

public sealed class SkillDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("cooldown")]
    public float Cooldown { get; set; }

    [JsonPropertyName("power")]
    public int Power { get; set; }

    [JsonPropertyName("reuse")]
    public int Reuse { get; set; }
}