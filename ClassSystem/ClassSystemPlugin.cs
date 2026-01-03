using ClassSystem.Menus;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Menu;
using MenuManager;         // dla IMenuManager
using Microsoft.Extensions.Logging;

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

        public override void Load(bool hotReload)
        {
            // Stwórz obiekty, zainicjuj cache, przygotuj słowniki itd.
            _classMenu = new ClassMenu();


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
            _classMenu.SetPlugin(this);
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

    }
}
