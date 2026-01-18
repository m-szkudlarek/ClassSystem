using System.Text.Json.Serialization;

namespace ClassSystem.Configuration;

public sealed class SkillDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("cooldown")]
    public float Cooldown { get; set; }

    [JsonPropertyName("heal")]
    public int Heal { get; set; }

    [JsonPropertyName("range")]
    public float? Range { get; set; }
}