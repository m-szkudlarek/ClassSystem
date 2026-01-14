using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace ClassSystem.Configuration;

public static class ClassConfigLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
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
            var classes = DeserializeClasses(json, logger);

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
        catch (IOException ex)
        {
            logger.LogInformation(ex, "[DEBUG] Błąd IO podczas czytania {Path}. Używam domyślnych klas.", configPath);
            return defaultClasses;
        }
        catch (JsonException ex)
        {
            logger.LogInformation(ex, "[DEBUG] Błąd parsowania pliku {Path}. Używam domyślnych klas.", configPath);
            return defaultClasses;
        }
    }

    private static void TryWriteDefaultConfig(string configPath, IEnumerable<ClassInfo> defaults)
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
            // Ignorujemy – jeśli nie możemy zapisać, plugin dalej zadziała na domyślnych.
        }
    }
    private static List<ClassInfo>? DeserializeClasses(string json, ILogger logger)
    {
        try
        {
            var classes = JsonSerializer.Deserialize<List<ClassInfo>>(json, SerializerOptions);
            if (classes == null)
            {
                logger.LogInformation("[DEBUG] Plik klas musi zawierać tablicę obiektów.");
                return null;
            }

            classes.ForEach(NormalizeClass);

            return classes;
        }
        catch (JsonException)
        {
            throw;
        }
    }

    private static void NormalizeClass(ClassInfo classInfo)
    {
        classInfo.Stats ??= new ClassStats();
        classInfo.Loadout ??= new List<string>();
        classInfo.Skills ??= new List<string>();
        classInfo.Stats.Normalize();
    }

    private static List<ClassInfo> GetDefaultClasses() =>
    [
        new ClassInfo
        {
            Id = "rambo",
            Name = "Rambo",
            Loadout = ["negev", "scythe"],
            Skills = [],
            Stats = new ClassStats
            {
                Hp = 130,
                Speed = 0.80f,
                DamageMultiplier = 1.1f
            }
        }
    ];
}
