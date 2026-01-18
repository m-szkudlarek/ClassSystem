using ClassSystem.Configuration;

namespace ClassSystem.Skills;

public static class SkillFactory
{
    public static IClassSkill CreateSkill(SkillDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        return definition.Id switch
        {
            "heal_ally" => new HealAllySkill(definition),
            "self_heal" => new SelfHealSkill(definition),
            _ => throw new InvalidOperationException($"Unknown skill id '{definition.Id}'.")
        };
    }
}