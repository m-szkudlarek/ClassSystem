using ClassSystem.Configuration;
using CounterStrikeSharp.API.Core;

namespace ClassSystem.Skills;

public sealed class SelfHealSkill : BaseSkill
{
    public SelfHealSkill(SkillDefinition definition)
        : base(definition)
    {
    }

    protected override bool Execute(CCSPlayerController caster, CCSPlayerController? target)
    {
        var pawn = caster.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid)
        {
            return false;
        }

        var newHealth = Math.Min(pawn.Health + Definition.Heal, pawn.MaxHealth);
        pawn.Health = newHealth;
        return true;
    }
}