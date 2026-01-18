using ClassSystem.Skills;

namespace ClassSystem.Runtime;

public sealed class RuntimeClass
{
    public RuntimeClass(ClassInfo definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Skills = definition.Skills.Select(SkillFactory.CreateSkill).ToList();
    }

    public ClassInfo Definition { get; }
    public IReadOnlyList<IClassSkill> Skills { get; }
}