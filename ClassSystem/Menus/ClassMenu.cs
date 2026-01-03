using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using MenuManager;
using Microsoft.Extensions.Logging;


namespace ClassSystem.Menus;

public sealed class ClassMenu
{
    private IMenuApi? _api;
    private ILogger? _logger;
    private BasePlugin? _plugin;


    // dostępne klasy (na start na sztywno)
    private static readonly HashSet<string> _validClassIds = new()
    {
        "assault",
        "tank",
        "scout"
    };
    // steam64 -> classId
    private readonly Dictionary<ulong, string> _selectedClass = new();

    // lista klas (na start prosta)
    private readonly (string Id, string Name, string Desc)[] _classes =
    {
        ("assault", "Szturmowiec", "Uniwersalny (startowa klasa)"),
        ("tank", "Tank", "Więcej HP, wolniej"),
        ("scout", "Scout", "Szybciej, mniej HP"),
    };
    //***********************************Setters*******************************
    public void SetApi(IMenuApi? menuManager)
    {
        if (_logger == null) return;
        _api =menuManager;
        if (_api == null) _logger.LogInformation("[DEBUG] ClassMenu API ustawione!");
        _logger.LogInformation("[DEBUG] ClassMenu API ustawione!");
    }

    public void SetLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void SetPlugin(BasePlugin plugin)
    {
        _plugin = plugin;
    }

    // ************************************FUNCKJE MENU*******************************

    public void ShowButtonClassMenu(CCSPlayerController player)
    {
        if (_api == null) return;     // jeśli API nie podpięte
        if (player == null || !player.IsValid) return;
        if (player.IsBot) return;
        if (_logger == null) return;

        // SteamID gracza
        ulong steam64 = player.SteamID;

        //_logger.LogInformation("[DEBUG] Tworzymy menu");
        // tworzysz nowe menu buttonowe
        var menu = _api.GetMenuForcetype("Wybierz klasę", MenuType.ButtonMenu);

        // dodajesz opcje — każda AddOption to nowy przycisk w menu
        foreach (var cls in _classes)
        {
            string classId = cls.Id;
            string className = cls.Name;
            string classDesc = cls.Desc;

            // label wyświetlany w menu
            string label = $"{className} — {classDesc}";

            menu.AddMenuOption(label, (p, option) =>
            {
                // zapis wyboru
                _selectedClass[steam64] = classId;

                // feedback dla gracza
                p.PrintToChat($"[ClassSystem] Wybrano klasę: {className}");

                // zamyka menu po wyborze
                _api.CloseMenu(p);
            });
        }

        // opcjonalnie możesz dodać linię „Wyjdź” na dole
        menu.AddMenuOption("Wyjdź", (p, option) =>
        {
            _api.CloseMenu(p);
        });

        menu.Open(player);

    }

}
