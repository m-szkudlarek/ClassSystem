using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Menu;
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

    public void ApplyClass(CCSPlayerController player, ClassInfo info)
    {
        if (player == null || !player.IsValid)
            return;

        var steamId = player.SteamID;
        _selectedClass[steamId] = info.Id;

        player.PrintToChat($"Wybrano klasę: {info.Name}");
        _logger?.LogInformation("[DEBUG] Gracz {Player} ({SteamId}) wybrał klasę {ClassId}", player.PlayerName, steamId, info.Id);
    }


}
