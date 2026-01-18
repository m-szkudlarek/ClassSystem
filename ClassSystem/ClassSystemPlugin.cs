using ClassSystem.Configuration;
using ClassSystem.Menus;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using MenuManager;         // dla IMenuManager
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Numerics;
using static CounterStrikeSharp.API.Core.Listeners;

namespace ClassSystem
{
    [MinimumApiVersion(80)]
    public sealed class ClassSystemPlugin : BasePlugin
    {
        public override string ModuleName => "ClassSystem";
        public override string ModuleVersion => "0.1.0";
        public override string ModuleAuthor => "kerzixa";


        //------------------------Capabilitys------------------------
        private readonly PluginCapability<IMenuApi?> _menuCap = new("menu:nfcore");

        //------------------------Fields------------------------
        private ClassMenu _classMenu = default!;
        private List<ClassInfo> _classes = [];
        private readonly HashSet<SteamID> _registered = [];  // “zarejestrowani w tej sesji”
        private readonly HashSet<ulong> _selectedThisRound = [];
        private bool _classSelectionOpen;
        private const int FreezeTimeSeconds = 20;
        private const float ClassSelectionWindowSeconds = FreezeTimeSeconds;
        private bool _restartAllowed = true;
        private readonly Dictionary<int, SteamID> _slotToSteamId = [];
        private int _classSelectionToken = 0;


        // === Medic skill constants ===
        private const string MedicSelfHealSkill = "self_heal";
        private readonly Dictionary<ulong, float> _medicHealCooldowns = [];

        // === Plugin lifecycle ===
        public override void Load(bool hotReload)
        {
            // Inicjalizacja konfiguracji i stanu.
            _classMenu = new ClassMenu();
            _classes = ClassConfigLoader.LoadOrCreate(ModuleDirectory, Logger);
            _classMenu.SetLogger(Logger);
            _classMenu.SetClasses(_classes);
            _classMenu.ClassApplied += OnClassApplied;

            // Rejestracja listenerów i eventów.
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
            RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            RegisterEventHandler<EventRoundFreezeEnd>(OnEventFreezeEnd);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);


            RegisterListener<Listeners.OnPlayerTakeDamagePre>(OnPlayerTakeDamagePre);
            AddCommandListener("jointeam", OnJoinTeam, HookMode.Pre);


        }

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            // Po wczytaniu pluginów konfigurujemy API menu i wyłączamy rozgrzewkę.
            Logger.LogInformation("[DEBUG] Próba pobrania api");
            var plugin = _menuCap.Get();

            if (plugin == null)
            {
                Logger.LogInformation("[DEBUG] MenuManager nie znaleziono...");
                return;
            }
            _classMenu.SetApi(plugin);
        }

        public void OnClientAuthorized(int playerSlot, SteamID steamId) {

            if (_registered.Add(steamId))
            {
                Logger.LogInformation($"[DEBUG]Zarejestrowano gracza: {steamId}");

            }
        }

        public void OnClientPutInServer(int playerSlot) {

            Logger.LogInformation("[DEBUG] Gracz dołączył do serwera");
            CCSPlayerController? ccsPlayerController = Utilities.GetPlayerFromSlot(playerSlot);
            if (ccsPlayerController == null || !ccsPlayerController.IsValid || ccsPlayerController.IsBot)
                return;

            SteamID? steam64 = ccsPlayerController.AuthorizedSteamID;
            if (steam64 is null)
                return;

            _slotToSteamId[playerSlot] = steam64;

            if (!_slotToSteamId.TryGetValue(playerSlot, out var steamId))
                return;

            AddTimer(0.2f, () =>
            {

                if (ccsPlayerController.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist)
                    return;

                EnsureBalancedTeam(ccsPlayerController); // Twoja logika
                RestartIfNeeded();          // tylko 1–2 graczy
            });

            //POWITANIE
            ccsPlayerController.PrintToChat($"Witaj, {ccsPlayerController.PlayerName}!");
            ccsPlayerController.PrintToChat($"Wybierz klasę, komend !klasa ");

        }

        public void OnMapStart(string mapName)
        {
            // Konfiguracja ustawień serwera po starcie mapy.
            Logger.LogInformation("[DEBUG] Konfiguracja rozgrzewki");

        }


        private HookResult OnJoinTeam(CCSPlayerController? player, CommandInfo info)
        {
            /*Logger.LogInformation("[DEBUG] Gracz próbuje zmienić drużynę.");
            if (player == null || !player.IsValid || player.IsBot)
            {
                return HookResult.Continue;
            }

            Logger.LogInformation("[DEBUG] Gracz próbuje zmienić drużynę.Za ifem");
            // Zablokuj ręczny wybór drużyny - wymuszamy balans.
            EnsureBalancedTeam(player);
            RestartIfNeeded();

            return HookResult.Handled;*/

            return HookResult.Continue;
        }

        // === Event handlers ===
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

        private HookResult OnPlayerSpawn(EventPlayerSpawn ev, GameEventInfo info)
        {
            Logger.LogInformation("[DEBUG] Gracz odrodził się - OnPlayerSpawn");

            /* var player = ev.Userid;
             if (player == null || !player.IsValid || player.IsBot)
                 return HookResult.Continue;

             // spawn bywa zanim pawn jest gotowy – daj krótki delay
             AddTimer(0.5f, () =>
             {
                 // PlayerPawn to CHandle – sprawdzaj IsValid / Value
                 if (!player.PlayerPawn.IsValid || player.PlayerPawn.Value == null)
                     return;

                 // Wczytaj zapisaną klasę z poprzednich rund.
                 _classMenu?.ApplySavedClass(player);

             });*/

            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
        {
            Logger.LogInformation("[DEBUG] Runda rozpoczęta - OnRoundStart");
            // Okno wyboru klas tylko na starcie rundy.
            _classSelectionOpen = true;
            _selectedThisRound.Clear();

            return HookResult.Continue;
        }

        private HookResult OnEventFreezeEnd(EventRoundFreezeEnd ev, GameEventInfo info)
        {
            Logger.LogInformation("[DEBUG] Koniec czasu zamrożenia rundy - OnEventFreezeEnd");
            // Zamknij okno wyboru klas po zakończeniu czasu zamrożenia.

            _classSelectionOpen = false;
            return HookResult.Continue;
        }

        private void OnClientDisconnect(int playerSlot)
        {
            // Sprawdź czy znamy ten slot
            if (!_slotToSteamId.TryGetValue(playerSlot, out var steamId))
            {
                // Slot nie był zarejestrowany (np. bot / reconnect glitch)
                return;
            }

            // Usuń mapowanie slot → SteamID
            _slotToSteamId.Remove(playerSlot);
            // 🔑 KLUCZOWE: pozwól na restart przy następnym wejściu
            _restartAllowed = true;

            Logger.LogInformation(
                $"[DEBUG] Player {steamId} left (slot {playerSlot})"
            );
        }

        private void OnClassApplied(CCSPlayerController player, ClassInfo info)
        {
            _selectedThisRound.Add(player.SteamID);
        }



        // === Balans drużyn / reset ===
        private void EnsureBalancedTeam(CCSPlayerController player)
        {
            var players = Utilities.GetPlayers()
                .Where(p => p != null && p.IsValid && !p.IsBot && p.SteamID != player.SteamID);

            var ctCount = players.Count(p => p.Team == CsTeam.CounterTerrorist);
            var ttCount = players.Count(p => p.Team == CsTeam.Terrorist);

            CsTeam desiredTeam = CsTeam.Terrorist;

            if (ttCount > ctCount)
            {
                desiredTeam = CsTeam.CounterTerrorist;
            }

            Logger.LogInformation($"[INFO] Zmieniam druzyne gracza {player.PlayerName} na {desiredTeam}");
            player.ChangeTeam(desiredTeam);
        }

        private void RestartIfNeeded()
        {
            int count = Utilities.GetPlayers()
                .Count(p => p.IsValid &&
                            !p.IsBot &&
                            (p.Team == CsTeam.CounterTerrorist ||
                             p.Team == CsTeam.Terrorist));

            if (count == 1 || count == 2)
                _restartAllowed = true;

            if ((count == 1 || count == 2) && _restartAllowed)
            {
                _restartAllowed = false;

                Logger.LogInformation(
                    $"[FLOW] Restarting game for {count} players"
                );

                Server.ExecuteCommand("mp_restartgame 1");
            }
        }

        // === Wybór klas ===
        private bool CanSelectClass(CCSPlayerController player)
        {
            if (!_classSelectionOpen)
            {
                player.PrintToChat("Wybór klasy jest możliwy tylko na początku rundy.");
                return false;
            }

            if (_selectedThisRound.Contains(player.SteamID))
            {
                player.PrintToChat("Klasa została już wybrana w tej rundzie.");
                return false;
            }

            return true;
        }

        [ConsoleCommand("css_klasa", "Otwiera menu klas")]
        public void CmdOpenClassMenu(CCSPlayerController? player, CommandInfo info)
        {

            if (player == null || !player.IsValid || player.IsBot)
                return;

            if (!_classMenu.HasApi())
            {
                Logger.LogInformation("[DEBUG] ClassMenuAPI nie udało sie pobrać");
                return;
            }

            if (!CanSelectClass(player))
            {
                return;
            }
            _classMenu.ShowButtonClassMenu(player);
        }

        [ConsoleCommand("css_selfheal", "Medyk: samoleczenie")]
        public void CommandMedicSelfHeal(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.IsBot)
                return;

            if (!_classMenu.TryGetSelectedClass(player.SteamID, out var classInfo) || classInfo == null)
            {
                player.PrintToChat("Najpierw wybierz klasę.");
                return;
            }

            var selfHealSkill = classInfo.Skills.FirstOrDefault(skill =>
                skill.Id.Equals(MedicSelfHealSkill, StringComparison.OrdinalIgnoreCase));

            if (selfHealSkill == null)
            {
                player.PrintToChat("Ta klasa nie posiada umiejętności samoleczenia.");
                return;
            }

            if (!player.PlayerPawn.IsValid || player.PlayerPawn.Value == null)
            {
                Logger.LogWarning("[DEBUG] Nie można uleczyć gracza {Player} – pawn niedostępny", player.PlayerName);
                return;
            }

            var now = Server.CurrentTime;
            if (_medicHealCooldowns.TryGetValue(player.SteamID, out var nextUseTime) && nextUseTime > now)
            {
                var remaining = nextUseTime - now;
                player.PrintToChat($"Umiejętność samoleczenia będzie dostępna za {remaining:0.0}s.");
                return;
            }

            var pawn = player.PlayerPawn.Value;
            var newHealth = Math.Min(pawn.MaxHealth, pawn.Health + selfHealSkill.Heal);
            pawn.Health = newHealth;
            _medicHealCooldowns[player.SteamID] = now + selfHealSkill.Cooldown;

            try
            {
                player.GiveNamedItem("weapon_healthshot");
                player.ExecuteClientCommandFromServer("use weapon_healthshot");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[DEBUG] Nie udało się uruchomić animacji strzykawki dla {Player}", player.PlayerName);
            }

            player.PrintToChat($"Uleczono: +{selfHealSkill.Heal} HP.");
        }
    


        [ConsoleCommand("css_test", "testowanie")]
        public void CommandTest(CCSPlayerController? player, CommandInfo info)
        {

            if (player == null || !player.IsValid || player.IsBot)
                return;
            Server.ExecuteCommand("mp_warmuptime 0");
            Server.ExecuteCommand("mp_warmup_end");
            Server.ExecuteCommand($"mp_freezetime {FreezeTimeSeconds}");

        }
    }
}