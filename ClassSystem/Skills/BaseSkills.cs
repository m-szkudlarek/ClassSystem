using ClassSystem.Configuration;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace ClassSystem.Skills;

public abstract class BaseSkill : IClassSkill
{
    protected BaseSkill(SkillDefinition definition)
    {
        Definition = definition;
        ResetRound();
    }

    protected SkillDefinition Definition { get; }

    public string Id => Definition.Id;
    public string Name => Definition.Id;

    public float Cooldown => Definition.Cooldown;
    public float LastUseTime { get; private set; } = float.NegativeInfinity;

    public int RemainingUses { get; private set; }

    public bool CanUse(CCSPlayerController caster)
    {
        if (caster == null || !caster.IsValid)
        {
            return false;
        }

        // cooldown
        if (Server.CurrentTime - LastUseTime < Cooldown)
        {
            return false;
        }

        // reuse (-1 = infinite)
        if (Definition.Reuse >= 0 && RemainingUses <= 0)
        {
            return false;
        }

        return true;
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

        if (Definition.Reuse >= 0)
        {
            RemainingUses--;
        }

        return true;
    }

    public void ResetRound()
    {
        RemainingUses = Definition.Reuse;
        LastUseTime = float.NegativeInfinity;
    }

    protected abstract bool Execute(
        CCSPlayerController caster,
        CCSPlayerController? target
    );
}
