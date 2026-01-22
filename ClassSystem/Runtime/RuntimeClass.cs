using ClassSystem.Skills;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;

namespace ClassSystem.Runtime;

public sealed class RuntimeClass
{
    public int UserId { get; }
    public string ClassId { get; }

    private readonly List<IClassSkill> _skills;

    public RuntimeClass(
        int userId,
        string classId,
        IEnumerable<IClassSkill> skills)
    {
        UserId = userId;
        ClassId = classId;
        _skills = [.. skills];
    }

    public IReadOnlyList<IClassSkill> Skills => _skills;

    public IClassSkill? GetSkill(string skillId)
    {
        return _skills.FirstOrDefault(
            s => s.Id.Equals(skillId, StringComparison.OrdinalIgnoreCase)
        );
    }

    public void ResetRound()
    {
        foreach (var skill in _skills)
        {
            skill.ResetRound();
        }
    }
}
