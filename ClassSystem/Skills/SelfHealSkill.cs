using ClassSystem.Configuration;
using CounterStrikeSharp.API.Core;

namespace ClassSystem.Skills;

public sealed class SelfHealSkill : BaseSkill
{
    public SelfHealSkill(SkillDefinition definition)
        : base(definition)
    {
    }

    protected override bool Execute(
        CCSPlayerController caster,
        CCSPlayerController? target)
    {
        if (!caster.PlayerPawn.IsValid || caster.PlayerPawn.Value == null)
            return false;

        var pawn = caster.PlayerPawn.Value;

        var healAmount = Definition.Power;
        if (healAmount <= 0)
            return false;

        var newHealth = Math.Min(
            pawn.MaxHealth,
            pawn.Health + healAmount
        );

        if (newHealth <= pawn.Health)
            return false;

        pawn.Health = newHealth;

        caster.PrintToChat($"Uleczono: +{newHealth - pawn.Health} HP");
        return true;
    }
}