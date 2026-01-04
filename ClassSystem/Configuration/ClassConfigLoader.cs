using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ClassSystem.Configuration;

public static class ClassConfigLoader
{
    public static List<ClassInfo> LoadOrCreate(string moduleDirectory, ILogger logger)
    {
        var defaultClasses = GetDefaultClasses();
        var configPath = Path.Combine(moduleDirectory, "klasy.json");

        if (!File.Exists(configPath))
        {
            logger.LogInformation("[DEBUG] Plik {path} nie istnieje. Tworzę z domyślnymi klasami.", configPath);
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
                logger.LogInformation("[DEBUG] Plik nie zawiera żadnych klas. Używam domyślnych.");
                return defaultClasses;
            }

            var uniqueClasses = classes
                .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                .GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            logger.LogInformation("[DEBUG] Załadowano {Count} klas.", uniqueClasses.Count);
            return uniqueClasses;
        }
        catch (JsonException ex)
        {
            logger.LogInformation(ex, "[DEBUG] Błąd parsowania pliku {Path}. Używam domyślnych klas.", configPath);
            return defaultClasses;
        }
        catch (IOException ex)
        {
            logger.LogInformation(ex, "[DEBUG] Błąd IO podczas czytania {Path}. Używam domyślnych klas.", configPath);
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
            Hp = 100,
            Speed = 1.0f,
            Damage = 1.0f,
            Loadout = ["rifle_ak47", "pistol_glock", "grenade_he"]
        },
    ];
}