using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ClassSystem.Configuration;

public static class SkillsConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static List<SkillDefinition> LoadOrCreate(string moduleDirectory, ILogger logger)
    {
        var defaultSkills = GetDefaultSkills();
        var configPath = Path.Combine(moduleDirectory, "skills.json");

        if (!File.Exists(configPath))
        {
            logger.LogInformation(
                "[DEBUG] Plik {Path} nie istnieje. Tworzę z domyślnymi skillami.",
                configPath
            );

            TryWriteDefaultConfig(configPath, defaultSkills);
            return defaultSkills;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var skills = DeserializeSkills(json, logger);

            if (skills == null || skills.Count == 0)
            {
                logger.LogInformation(
                    "[DEBUG] Plik skills.json nie zawiera żadnych skilli. Używam domyślnych."
                );
                return defaultSkills;
            }

            var uniqueSkills = skills
                .Where(s => !string.IsNullOrWhiteSpace(s.Id))
                .GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            logger.LogInformation(
                "[DEBUG] Załadowano {Count} skilli.",
                uniqueSkills.Count
            );

            return uniqueSkills;
        }
        catch (IOException ex)
        {
            logger.LogInformation(
                ex,
                "[DEBUG] Błąd IO podczas czytania {Path}. Używam domyślnych skilli.",
                configPath
            );
            return defaultSkills;
        }
        catch (JsonException ex)
        {
            logger.LogInformation(
                ex,
                "[DEBUG] Błąd parsowania pliku {Path}. Używam domyślnych skilli.",
                configPath
            );
            return defaultSkills;
        }
    }

    private static void TryWriteDefaultConfig(
        string configPath,
        IEnumerable<SkillDefinition> defaults)
    {
        try
        {
            var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(configPath, json);
        }
        catch (Exception)
        {
            // Celowo ignorujemy – plugin dalej zadziała na domyślnych skillach
        }
    }

    private static List<SkillDefinition>? DeserializeSkills(
        string json,
        ILogger logger)
    {
        try
        {
            var skills = JsonSerializer.Deserialize<List<SkillDefinition>>(
                json,
                SerializerOptions
            );

            if (skills == null)
            {
                logger.LogInformation(
                    "[DEBUG] Plik skills.json musi zawierać tablicę obiektów."
                );
                return null;
            }

            skills.ForEach(NormalizeSkill);
            return skills;
        }
        catch (JsonException)
        {
            throw;
        }
    }

    private static void NormalizeSkill(SkillDefinition skill)
    {
        skill.Id ??= string.Empty;

        // Zabezpieczenia minimalne
        if (skill.Cooldown < 0)
            skill.Cooldown = 0;

        if (skill.Reuse == 0)
            skill.Reuse = 1;
    }

    private static List<SkillDefinition> GetDefaultSkills() =>
    [
        new SkillDefinition
        {
            Id = "self_heal",
            Cooldown = 20,
            Reuse = 1,
            Power = 50
        }
    ];
}
