using ClassSystem.Configuration;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace ClassSystem.Skills;

public abstract class BaseSkill : IClassSkill
{
    protected BaseSkill(SkillDefinition definition)
    {
        Definition = definition;
    }

    protected SkillDefinition Definition { get; }

    public string Id => Definition.Id;
    public string Name => Definition.Id;
    public float Cooldown => Definition.Cooldown;
    public float LastUseTime { get; private set; } = float.NegativeInfinity;

    public bool CanUse(CCSPlayerController caster)
    {
        if (caster == null || !caster.IsValid)
        {
            return false;
        }

        return Server.CurrentTime - LastUseTime >= Cooldown;
    }

    public bool Use(CCSPlayerController caster, CCSPlayerController? target)
    {
        if (!CanUse(caster))
        {
            return false;
        }

        if (!Execute(caster, target))
        {
            return false;
        }

        LastUseTime = Server.CurrentTime;
        return true;
    }

    protected abstract bool Execute(CCSPlayerController caster, CCSPlayerController? target);
}