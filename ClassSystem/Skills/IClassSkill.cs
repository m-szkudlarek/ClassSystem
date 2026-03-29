using CounterStrikeSharp.API.Core;

namespace ClassSystem.Skills;

public interface IClassSkill
{
    // Identyfikacja
    string Id { get; }
    string Name { get; }

    // Cooldown
    float Cooldown { get; }
    float LastUseTime { get; }

    // Reuse / charges
    int RemainingUses { get; }

    // Logika użycia
    bool CanUse(CCSPlayerController caster);
    bool Use(CCSPlayerController caster, CCSPlayerController? target);

    // Reset stanu (np. nowa runda)
    void ResetRound();
}