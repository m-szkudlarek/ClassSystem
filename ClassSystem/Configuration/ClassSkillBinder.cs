using ClassSystem.Configuration;
using Microsoft.Extensions.Logging;

namespace ClassSystem.Skills;

public static class ClassSkillBinder
{
    /// <summary>
    /// Łączy ID skilli z klas (string) z definicjami skilli.
    /// Nie tworzy runtime obiektów – tylko waliduje i mapuje dane.
    /// </summary>
    public static Dictionary<string, List<SkillDefinition>> Bind(
        IEnumerable<ClassDefinition> classes,
        IEnumerable<SkillDefinition> skills,
        ILogger logger)
    {
        // Słownik wszystkich skilli po ID
        var skillMap = skills
            .Where(s => !string.IsNullOrWhiteSpace(s.Id))
            .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.First(),
                StringComparer.OrdinalIgnoreCase
            );

        // Wynik: classId -> lista definicji skilli
        var result = new Dictionary<string, List<SkillDefinition>>(
            StringComparer.OrdinalIgnoreCase
        );

        foreach (var classInfo in classes)
        {
            var resolvedSkills = new List<SkillDefinition>();

            foreach (var skillId in classInfo.Skills)
            {
                if (skillMap.TryGetValue(skillId, out var skill))
                {
                    resolvedSkills.Add(skill);
                }
                else
                {
                    logger.LogInformation(
                        "[DEBUG] Klasa '{ClassId}' odwołuje się do nieistniejącego skilla '{SkillId}'.",
                        classInfo.Id,
                        skillId
                    );
                }
            }

            result[classInfo.Id] = resolvedSkills;
        }

        logger.LogInformation("[DEBUG] Zbindowano skille. ");

        return result;
    }
}
