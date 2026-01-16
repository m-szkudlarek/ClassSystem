using System.Text.Json.Serialization;

namespace ClassSystem;

public sealed class ClassStats
{
    [JsonPropertyName("hp")]
    public int Hp { get; set; } = 100;

    [JsonPropertyName("speed")]
    public float Speed { get; set; } = 1.0f;

    [JsonPropertyName("damageMultiplier")]
    public float DamageMultiplier { get; set; } = 1.0f;

    public void Normalize()
    {
        // miejsce na ewentualną przyszłą normalizację
    }
}