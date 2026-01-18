using ClassSystem.Configuration;
using CounterStrikeSharp.API.Core;
using System.Numerics;

namespace ClassSystem.Skills;

public sealed class HealAllySkill : BaseSkill
{
    public HealAllySkill(SkillDefinition definition)
        : base(definition)
    {
    }

    protected override bool Execute(CCSPlayerController caster, CCSPlayerController? target)
    {
        if (target == null || !target.IsValid || target.IsBot)
        {
            return false;
        }

        if (!TryGetPlayerOrigin(caster, out var casterOrigin) ||
            !TryGetPlayerOrigin(target, out var targetOrigin))
        {
            return false;
        }

        if (Definition.Range.HasValue)
        {
            var distance = Vector3.Distance(casterOrigin, targetOrigin);
            if (distance > Definition.Range.Value)
            {
                return false;
            }
        }

        var targetPawn = target.PlayerPawn.Value;
        if (targetPawn == null || !targetPawn.IsValid)
        {
            return false;
        }

        var newHealth = Math.Min(targetPawn.Health + Definition.Heal, targetPawn.MaxHealth);
        targetPawn.Health = newHealth;
        return true;
    }

    private static bool TryGetPlayerOrigin(CCSPlayerController player, out Vector3 origin)
    {
        origin = default;
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid)
        {
            return false;
        }

        origin = (Vector3)(pawn.AbsOrigin ?? default);
        return true;
    }
}