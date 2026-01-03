using System.Text.Json.Serialization;

namespace ClassSystem;

public sealed class ClassInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("desc")]
    public string Desc { get; set; } = string.Empty;

    [JsonPropertyName("hp")]
    public int Hp { get; set; } = 100;

    [JsonPropertyName("speed")]
    public float Speed { get; set; } = 1.0f;

    [JsonPropertyName("damage")]
    public float Damage { get; set; } = 1.0f;

    [JsonPropertyName("loadout")]
    public List<string> Loadout { get; set; } = new();
}
