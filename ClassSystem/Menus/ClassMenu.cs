using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using MenuManager;
using Microsoft.Extensions.Logging;

namespace ClassSystem.Menus;

public sealed class ClassMenu
{
    private IMenuApi? _api;
    private ILogger? _logger;

    private readonly Dictionary<ulong, string> _selectedClass = [];
    private readonly Dictionary<string, ClassInfo> _classLookup = new(StringComparer.OrdinalIgnoreCase);
    private List<ClassInfo> _classes = [];
    public IReadOnlyDictionary<ulong, string> GetSelections() => _selectedClass;
    public bool HasClass(string classId) => _classLookup.ContainsKey(classId);
    public bool TryGetSelectedClass(ulong steamId, out ClassInfo? info)
    {
        info = null;

        if (!_selectedClass.TryGetValue(steamId, out var classId))
        {
            return false;
        }

        if (!_classLookup.TryGetValue(classId, out info))
        {
            return false;
        }

        return true;
    }

    //***********************************Setters*******************************
    public void SetApi(IMenuApi? menuManager)
    {
        _api = menuManager;
        _logger?.LogInformation("[DEBUG] ClassMenu API ustawione!");
    }

    public void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void SetClasses(IEnumerable<ClassInfo> classes)
    {
        _classes = [.. classes.Where(cls => !string.IsNullOrWhiteSpace(cls.Id))];

        _classLookup.Clear();
        foreach (var cls in _classes)
        {
            _classLookup[cls.Id] = cls;
        }
    }

    // ************************************ FUNCKJE *******************************

    public void ShowButtonClassMenu(CCSPlayerController player)
    {
        if (_api == null) return;     // jeśli API nie podpięte
        if (player == null || !player.IsValid) return;
        if (player.IsBot) return;
        if (_logger == null) return;

        if (_classes.Count == 0)
        {
            player.PrintToChat("[DEBUG] Brak dostępnych klas do wyboru.");
            return;
        }

        var menu = _api.GetMenuForcetype("Wybierz klasę", MenuType.ButtonMenu);
        var index = 0;


        // Dodaj opcje klas-tworzenie labela

        foreach (var cls in _classes)
        {
            string className = cls.Name;
            index++;
            

            string label = $"{index}.{className}";

            menu.AddMenuOption(label, (p, option) =>
            {
                ApplyClass(p, cls);

                _api.CloseMenu(p);
            });
        }

        menu.AddMenuOption("Wyjdź", (p, option) =>
        {
            _api.CloseMenu(p);
        });

        menu.Open(player);
    }

    public bool TryApplyClass(CCSPlayerController player, string classId, out ClassInfo? appliedInfo)
    {
        appliedInfo = null;

        if (!_classLookup.TryGetValue(classId, out var info))
        {
            return false;
        }

        ApplyClass(player, info);
        appliedInfo = info;
        return true;
    }

    public bool ApplySavedClass(CCSPlayerController player, bool announce = false)
    {
        if (!TryGetSelectedClass(player.SteamID, out var info) || info == null)
        {
            return false;
        }

        ApplyClassEffects(player, info, announce);
        return true;
    }

    public void ApplyClass(CCSPlayerController player, ClassInfo info)
    {
        if (player == null || !player.IsValid)
            return;

        var steamId = player.SteamID;
        _selectedClass[steamId] = info.Id;

        ApplyClassEffects(player, info, true);
    }

    private void ApplyClassEffects(CCSPlayerController player, ClassInfo info, bool announce)
    {
        if (player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !player.PlayerPawn.IsValid)
        {
            _logger?.LogWarning("[DEBUG] Nie można zastosować klasy {ClassId} – pawn gracza jest niedostępny", info.Id);
            return;
        }

        ApplyStats(pawn, info.Stats);
        GiveLoadout(player, info.Loadout);

        if (announce)
        {
            player.PrintToChat($"Wybrano klasę: {info.Name}");
            _logger?.LogInformation("[DEBUG] Gracz {Player} ({SteamId}) wybrał klasę {ClassId}", player.PlayerName, player.SteamID, info.Id);
        }
    }

    private void ApplyStats(CCSPlayerPawn pawn, ClassStats stats)
    {
        pawn.MaxHealth = stats.Hp;
        pawn.Health = stats.Hp;
        pawn.VelocityModifier = stats.Speed;
    }

    private void GiveLoadout(CCSPlayerController player, IReadOnlyCollection<string> loadout)
    {
        if (loadout.Count == 0)
        {
            return;
        }

        try
        {
            player.RemoveWeapons();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DEBUG] Nie udało się usunąć broni gracza {Player}", player.PlayerName);
        }

        foreach (var weaponName in loadout)
        {
            var normalizedName = NormalizeWeaponName(weaponName);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                continue;
            }

            try
            {
                player.GiveNamedItem(normalizedName);
                _logger?.LogInformation("[DEBUG] Nadano {Weapon} graczowi {Player}", normalizedName, player.PlayerName);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[DEBUG] Nie udało się nadać {Weapon} graczowi {Player}", normalizedName, player.PlayerName);
            }
        }
    }

    private string NormalizeWeaponName(string weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            return string.Empty;
        }

        var compactName = weaponName.Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        if (Enum.TryParse<CsItem>(compactName, true, out var csItem))
        {
            var enumValue = EnumUtils.GetEnumMemberAttributeValue(csItem);
            if (!string.IsNullOrWhiteSpace(enumValue))
            {
                return enumValue;
            }
        }

        var lowered = compactName.ToLowerInvariant();
        if (!lowered.StartsWith("weapon_", StringComparison.Ordinal) && !lowered.StartsWith("item_", StringComparison.Ordinal))
        {
            lowered = $"weapon_{lowered}";
        }

        return lowered;
    }
}

