using ClassSystem.Configuration;
using ClassSystem.Menus;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
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
        private List<ClassInfo> _classes = [];

        public override void Load(bool hotReload)
        {
            // Stwórz obiekty, zainicjuj cache, przygotuj słowniki itd.
            _classMenu = new ClassMenu();
            _classMenu.SetLogger(Logger);
            _classes = ClassConfigLoader.LoadOrCreate(ModuleDirectory, Logger);
            _classMenu.SetClasses(_classes);

            RegisterListener<Listeners.OnPlayerTakeDamagePre>(OnPlayerTakeDamagePre);

            RegisterEventHandler<EventPlayerSpawn>((ev, info) =>
            {
                var player = ev.Userid;
                if (player == null || !player.IsValid || player.IsBot)
                    return HookResult.Continue;

                // spawn bywa zanim pawn jest gotowy – daj krótki delay
                AddTimer(0.5f, () =>
                {
                    // PlayerPawn to CHandle – sprawdzaj IsValid / Value
                    if (!player.PlayerPawn.IsValid || player.PlayerPawn.Value == null)
                        return;

                    var steam64 = player.SteamID;

                    _classMenu?.ApplySavedClass(player);

                    // GUARD: nie rób rejestracji drugi raz
                    if (_registered.Contains(steam64))
                        return;

                    RegisterPlayer(player);
                });

                return HookResult.Continue;
            });
        }

        [ConsoleCommand("css_klasa", "Otwiera menu klas")]
        public void CmdOpenClassMenu(CCSPlayerController? player, CommandInfo info)
        {

            if (player == null || !player.IsValid || player.IsBot)
                return;

            if (_menuCap.Get() == null)
            {
                Logger.LogInformation("[DEBUG] ClassMenuAPI nie udało sie pobrać");
                return;
            }
            _classMenu.ShowButtonClassMenu(player);
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

        private HookResult OnPlayerTakeDamagePre(CCSPlayerPawn victim, CTakeDamageInfo info)
        {
            if (info == null || info.Attacker == null || !info.Attacker.IsValid)
            {
                return HookResult.Continue;
            }

            var attackerEntity = info.Attacker.Get();
            var attackerPawn = attackerEntity?.As<CCSPlayerPawn>();
            var attackerController = attackerPawn?.OriginalController?.Value;

            if (attackerController == null || !attackerController.IsValid || _classMenu == null)
            {
                return HookResult.Continue;
            }

            if (!_classMenu.TryGetSelectedClass(attackerController.SteamID, out var classInfo) || classInfo == null)
            {
                return HookResult.Continue;
            }

            info.Damage *= classInfo.Stats.DamageMultiplier;
            return HookResult.Continue;
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
}
