using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClassSystem.Configuration;

public static class ClassConfigLoader
{
    public static List<ClassInfo> LoadOrCreate(string moduleDirectory, ILogger logger)
    {
        var defaultClasses = GetDefaultClasses();
        var configPath = Path.Combine(moduleDirectory, "classes.json");

        if (!File.Exists(configPath))
        {
            logger.LogWarning("[ClassSystem] Plik {Path} nie istnieje. Tworzę z domyślnymi klasami.", configPath);
            TryWriteDefaultConfig(configPath, defaultClasses);
            return defaultClasses;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var classes = JsonSerializer.Deserialize<List<ClassInfo>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (classes == null || classes.Count == 0)
            {
                logger.LogWarning("[ClassSystem] Plik {Path} nie zawiera żadnych klas. Używam domyślnych.", configPath);
                return defaultClasses;
            }

            var uniqueClasses = classes
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            logger.LogInformation("[ClassSystem] Załadowano {Count} klas z {Path}.", uniqueClasses.Count, configPath);
            return uniqueClasses;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "[ClassSystem] Błąd parsowania pliku {Path}. Używam domyślnych klas.", configPath);
            return defaultClasses;
        }
        catch (IOException ex)
        {
            logger.LogError(ex, "[ClassSystem] Błąd IO podczas czytania {Path}. Używam domyślnych klas.", configPath);
            return defaultClasses;
        }
    }

    private static void TryWriteDefaultConfig(string configPath, IEnumerable<ClassInfo> defaults)
    {
        try
        {
            var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configPath, json);
        }
        catch (Exception)
        {
            // Ignorujemy – jeśli nie możemy zapisać, plugin dalej zadziała na domyślnych.
        }
    }

    private static List<ClassInfo> GetDefaultClasses() =>
    [
        new ClassInfo
        {
            Id = "assault",
            Name = "Szturmowiec",
            Desc = "Uniwersalny balans pomiędzy siłą i mobilnością",
            Hp = 100,
            Speed = 1.0f,
            Damage = 1.0f,
            Loadout = ["rifle_ak47", "pistol_glock", "grenade_he"]
        },
        new ClassInfo
        {
            Id = "tank",
            Name = "Tank",
            Desc = "Dużo HP kosztem prędkości",
            Hp = 140,
            Speed = 0.9f,
            Damage = 0.9f,
            Loadout = ["rifle_m249", "pistol_p250", "grenade_he", "armor_heavy"]
        },
        new ClassInfo
        {
            Id = "scout",
            Name = "Scout",
            Desc = "Szybki zwiadowca z lekką bronią",
            Hp = 90,
            Speed = 1.1f,
            Damage = 0.95f,
            Loadout = ["rifle_ssg08", "pistol_fiveseven", "smokegrenade"]
        }
    ];
}