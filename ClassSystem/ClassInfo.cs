using System.Text.Json.Serialization;

namespace ClassSystem;

public sealed class ClassInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("stats")]
    public ClassStats Stats { get; set; } = new();

    [JsonIgnore]
    public int Hp
    {
        get => Stats.Hp;
        set => Stats.Hp = value;
    }

    [JsonIgnore]
    public float Speed
    {
        get => Stats.Speed;
        set => Stats.Speed = value;
    }

    [JsonIgnore]
    public float DamageMultiplier
    {
        get => Stats.DamageMultiplier;
        set => Stats.DamageMultiplier = value;
    }

    [JsonPropertyName("loadout")]
    public List<string> Loadout { get; set; } = [];
}