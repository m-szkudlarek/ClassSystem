using ClassSystem.Menus;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Commands;
using MenuManager;         // dla IMenuManager
using Microsoft.Extensions.Logging;
using ClassSystem.Configuration;

namespace ClassSystem
{
    [MinimumApiVersion(80)]
    public sealed class ClassSystemPlugin : BasePlugin
    {
        public override string ModuleName => "ClassSystem";
        public override string ModuleVersion => "0.1.0";
        public override string ModuleAuthor => "kerzixa";


        private ClassMenu _classMenu = default!;
        private readonly HashSet<ulong> _registered = new();  // “zarejestrowani w tej sesji”
        private readonly PluginCapability<IMenuApi?> _menuCap = new("menu:nfcore");
        private List<ClassInfo> _classes = new();

        public override void Load(bool hotReload)
        {
            // Stwórz obiekty, zainicjuj cache, przygotuj słowniki itd.
            _classMenu = new ClassMenu();
            _classMenu.SetLogger(Logger);
            _classes = ClassConfigLoader.LoadOrCreate(ModuleDirectory, Logger);
            _classMenu.SetClasses(_classes);


            AddCommand("css_klasa", "Otwiera menu klas", (player, info) =>
            {
                if (player == null)
                    return;

                if (!player.IsValid || player.IsBot)
                    return;

                // jeśli API menu nie jest ustawione
                if (_menuCap.Get() == null)
                {
                    Logger.LogInformation("[DEBUG] ClassMenuAPI nie udało sie pobrać");
                    return;
                }

                _classMenu.ShowButtonClassMenu(player);

            });

            AddCommand("setclass", "Ustawia klasę po ID z czatu", (player, info) =>
            {
                if (player == null || !player.IsValid || player.IsBot)
                    return;

                if (info.ArgCount < 2)
                {
                    player.PrintToChat("[ClassSystem] Użycie: !setclass <id>");
                    return;
                }

                var classId = info.GetArg(1)?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(classId))
                {
                    player.PrintToChat("[ClassSystem] Podaj ID klasy z pliku classes.json.");
                    return;
                }

                if (!_classMenu.TryApplyClass(player, classId, out var applied))
                {
                    player.PrintToChat("[ClassSystem] Nieznana klasa. Sprawdź plik classes.json.");
                    return;
                }

                player.PrintToChat($"[ClassSystem] Ustawiono klasę: {applied.Name} ({applied.Id})");
            });

            RegisterEventHandler<EventPlayerSpawn>((ev, info) =>
            {
                var player = ev.Userid;
                if (player == null || !player.IsValid || player.IsBot)
                    return HookResult.Continue;

                // spawn bywa zanim pawn jest gotowy – daj krótki delay
                AddTimer(0.5f, () =>
                {
                    if (player == null || !player.IsValid || player.IsBot)
                        return;

                    // PlayerPawn to CHandle – sprawdzaj IsValid / Value
                    if (!player.PlayerPawn.IsValid || player.PlayerPawn.Value == null)
                        return;

                    var steam64 = player.SteamID;

                    // GUARD: nie rób rejestracji drugi raz
                    if (_registered.Contains(steam64))
                        return;                  

                    RegisterPlayer(player);
                    player.PrintToChat($"[DEBUG] Zarejestrowano tylko raz: {player.PlayerName} ({steam64})");
                });

                return HookResult.Continue;
            });
        }



        public override void OnAllPluginsLoaded(bool hotReload)
        {
            Logger.LogInformation("[DEBUG] Próba pobrania api");
            var plugin = _menuCap.Get();

            if (plugin == null)
            {
                Logger.LogInformation("[DEBUG] MenuManager Core not found...");
                return;
            }
            _classMenu.SetApi(plugin);
            _classMenu.SetLogger(Logger);
        }

        private void RegisterPlayer(CCSPlayerController player)
        {
            ulong steam64 = player.SteamID;
            // Use Add() directly and check its return value instead of Contains() + Add()
            if (_registered.Add(steam64))
            {
                player.PrintToChat($"Witaj, {player.PlayerName}!");
                Logger.LogInformation($"[DEBUG]Zarejestrowano gracza: {steam64} ({player.PlayerName})");

                // tutaj możesz od razu pokazywać menu klas
                if (_classMenu == null) return;
                _classMenu.ShowButtonClassMenu(player);
            }
        }

        private List<ClassInfo> LoadClassesFromJson()
        {
            // pozostawione dla zgodności/komentarzy – logika przeniesiona do ClassConfigLoader
            return ClassConfigLoader.LoadOrCreate(ModuleDirectory, Logger);
        }
}
