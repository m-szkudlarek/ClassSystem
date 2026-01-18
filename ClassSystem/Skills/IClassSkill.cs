using CounterStrikeSharp.API.Core;

namespace ClassSystem.Skills;

public interface IClassSkill
{
    string Id { get; }
    string Name { get; }
    float Cooldown { get; }
    float LastUseTime { get; }
    bool CanUse(CCSPlayerController caster);
    bool Use(CCSPlayerController caster, CCSPlayerController? target);
}