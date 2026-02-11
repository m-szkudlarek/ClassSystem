using ClassSystem.Configuration;
using ClassSystem.Menus;
using ClassSystem.Runtime;
using ClassSystem.Skills;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Capabilities;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using MenuManager;
using Microsoft.Extensions.Logging;

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
        private List<ClassDefinition> _classes = [];
        private List<SkillDefinition> _skills = [];
        private Dictionary<string, List<SkillDefinition>> _classSkillMap = [];

        private readonly HashSet<SteamID> _registered = [];  // “zarejestrowani w tej sesji”
        private readonly Dictionary<ulong, CsTeam> _pendingAutoTeam = []; //autobalans
        private bool _restartDoneForLowPlayers = false;
        private readonly HashSet<int> _selectedThisRound = [];
        private bool _classSelectionOpen;
        private const int FreezeTimeSeconds = 20;
        private const float ClassSelectionWindowSeconds = FreezeTimeSeconds;
        private bool _restartAllowed = true;
        private readonly Dictionary<int, ulong> _slotToSteamId = [];
        private int _classSelectionToken = 0;
        private readonly Dictionary<int, SteamID> _authorizedSteamIds = [];
        private readonly Dictionary<int, List<Action<SteamID>>> _pendingSteamActions = [];
        private readonly HashSet<string> _steamIdWarnings = [];
        private bool _steamApiReady;


        // === Skill constants ===
        private const string MedicSelfHealSkill = "self_heal";
        private readonly Dictionary<int, RuntimeClass> _runtimeClasses = [];

        // === Plugin lifecycle ===
        public override void Load(bool hotReload)
        {
            // Inicjalizacja konfiguracji i stanu.
            _classMenu = new ClassMenu();
            _classes = ClassConfigLoader.LoadOrCreate(ModuleDirectory, Logger);
            _skills = SkillsConfigLoader.LoadOrCreate(ModuleDirectory, Logger);
            _classSkillMap = ClassSkillBinder.Bind(_classes, _skills, Logger);
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
            RegisterListener<Listeners.OnGameServerSteamAPIActivated>(OnSteamApiActivated);
            RegisterListener<Listeners.OnGameServerSteamAPIDeactivated>(OnSteamApiDeactivated);


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

        public void OnClientAuthorized(int playerSlot, SteamID steamId)
        {
            Logger.LogInformation($"[DEBUG] OnClientAuthorized");
            _authorizedSteamIds[playerSlot] = steamId;
            _slotToSteamId[playerSlot] = steamId.SteamId64;
            DrainPendingSteamActions(playerSlot, steamId);
            if (_registered.Add(steamId))
            {
                Logger.LogInformation($"[INFO] Zarejestrowano gracza: {steamId}");

            }
        }

        public void OnClientPutInServer(int playerSlot)
        {

            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player == null || !player.IsValid || player.IsBot)
                return;

            Logger.LogInformation($"[INFO] Gracz dołączył do serwera {player.PlayerName} (slot={playerSlot})");

            player.PrintToChat($"Witaj, {player.PlayerName}!");
            player.PrintToChat($"Wybierz klasę, komend !klasa ");

            var steamId = SteamIdSafe(player, nameof(OnClientPutInServer));
            if (steamId !=null)
            {
                _slotToSteamId[playerSlot] = steamId.SteamId64;
            }
            else
            {
                EnqueueSteamAction(playerSlot, id => _slotToSteamId[playerSlot] = id.SteamId64);
            }
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

            if (!attackerController.UserId.HasValue)
            {
                return HookResult.Continue;
            }

            if (!_classMenu.TryGetSelectedClass(attackerController.UserId.Value, out var classInfo) || classInfo == null)
            {
                return HookResult.Continue;
            }

            info.Damage *= classInfo.Stats.DamageMultiplier;
            return HookResult.Continue;
        }

        private HookResult OnPlayerSpawn(EventPlayerSpawn ev, GameEventInfo info)
        {
            Logger.LogInformation("[DEBUG] Gracz odrodził się - OnPlayerSpawn");
            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
        {
            Logger.LogInformation("[DEBUG] Runda rozpoczęta - OnRoundStart");
            // Okno wyboru klas tylko na starcie rundy.
            _classSelectionOpen = true;
            _selectedThisRound.Clear();

            foreach (var runtime in _runtimeClasses.Values)
            {
                runtime.ResetRound();
            }

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
            _authorizedSteamIds.Remove(playerSlot);
            _pendingSteamActions.Remove(playerSlot);

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

        private void OnClassApplied(CCSPlayerController player, ClassDefinition info)
        {
            Logger.LogInformation("OnClassApplied");
            // 1️⃣ Oznacz, że gracz wybrał klasę w tej rundzie
            if (!player.UserId.HasValue)
            {
                Logger.LogWarning("[WARN] Brak UserId dla gracza {Player}. Nie można przypisać klasy.", player.PlayerName);
                return;
            }

            _selectedThisRound.Add(player.UserId.Value);

            // 2️⃣ Pobierz ID klasy
            var classId = info.Id;

            // 3️⃣ Sprawdź, czy mamy zbindowane skille dla tej klasy
            if (!_classSkillMap.TryGetValue(classId, out var skillDefinitions))
            {
                Logger.LogWarning(
                    "[WARN] Brak skilli dla klasy '{ClassId}' (gracz {Player})",
                    classId,
                    player.PlayerName
                );
                skillDefinitions = [];
            }

            // 4️⃣ Utwórz runtime skille przez SkillFactory
            var runtimeSkills = skillDefinitions
                .Select(SkillFactory.CreateSkill)
                .ToList();

            // 5️⃣ Utwórz RuntimeClass
            var runtimeClass = new RuntimeClass(
                player.UserId.Value,
                classId,
                runtimeSkills
            );

            // 6️⃣ Przypisz RuntimeClass do gracza (nadpisuje poprzednią, jeśli była)
            _runtimeClasses[player.UserId.Value] = runtimeClass;

            Logger.LogInformation(
                "[DEBUG] Przypisano klasę '{ClassId}' graczowi {Player} ({SkillCount} skilli)",
                classId,
                player.PlayerName,
                runtimeSkills.Count
            );
        }


        // === Wybór klas ===
        private bool CanSelectClass(CCSPlayerController player)
        {
            if (!_classSelectionOpen)
            {
                player.PrintToChat("Wybór klasy jest możliwy tylko na początku rundy.");
                return false;
            }

            if (!player.UserId.HasValue)
            {
                player.PrintToChat("Brak UserId - spróbuj ponownie za chwilę.");
                return false;
            }

            if (_selectedThisRound.Contains(player.UserId.Value))
            {
                player.PrintToChat("Klasa została już wybrana w tej rundzie.");
                return false;
            }

            return true;
        }

        [ConsoleCommand("css_klasa", "Otwiera menu klas")]
        public void CmdOpenClassMenu(CCSPlayerController? player, CommandInfo info)
        {

            Logger.LogInformation("[DEBUG] Wywołanie klas");
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

        private void GrantItemsForRuntime(CCSPlayerController player, RuntimeClass runtime)
        {
            // self_heal → healthshot
            if (runtime.GetSkill("self_heal") != null)
            {
                player.GiveNamedItem("weapon_healthshot");
            }

            // kolejne skille → kolejne itemy
        }

        [ConsoleCommand("css_test", "testowanie")]
        public void CommandTest(CCSPlayerController? player, CommandInfo info)
        {

            if (player == null || !player.IsValid || player.IsBot)
                return;

            // 1️⃣ Czy gracz ma RuntimeClass?
            if (!player.UserId.HasValue)
            {
                player.PrintToChat("❌ Brak UserId - spróbuj ponownie za chwilę.");
                return;
            }

            if (!_runtimeClasses.TryGetValue(player.UserId.Value, out var runtime))
            {
                player.PrintToChat("❌ Nie masz jeszcze wybranej klasy.");
                return;
            }

            // 2️⃣ Czy klasa ma skill self_heal?
            var skill = runtime.GetSkill("self_heal");
            if (skill == null)
            {
                player.PrintToChat("❌ Twoja klasa nie posiada umiejętności samoleczenia.");
                return;
            }

            // 3️⃣ Spróbuj użyć skilla
            var success = skill.Use(player, player);

            if (!success)
            {
                player.PrintToChat("⏳ Nie możesz teraz użyć tej umiejętności (cooldown lub brak użyć).");
            }
        }

        private void OnSteamApiActivated()
        {
            _steamApiReady = true;
            Logger.LogInformation("[DEBUG] SteamAPI activated.");
            DrainAllPendingSteamActions();
        }

        private void OnSteamApiDeactivated()
        {
            _steamApiReady = false;
            Logger.LogWarning("[DEBUG] SteamAPI deactivated.");
        }

        private SteamID? SteamIdSafe(CCSPlayerController? player, string where)
        {
            if (player == null || !player.IsValid)
            {
                return null;
            }

            if (_steamApiReady)
            {
                return player.AuthorizedSteamID;
            }

            var warningKey = $"{where}:{player.UserId}";
            if (_steamIdWarnings.Add(warningKey))
            {
                Logger.LogWarning(
                    "[STEAMAPI] Próba odczytu SteamID przed aktywacją SteamAPI w {Where} (slot={Slot}, userId={UserId}, name={Name})",
                    where,
                    player.Slot,
                    player.UserId,
                    player.PlayerName
                );
            }

            return null;
        }

        private void EnqueueSteamAction(int playerSlot, Action<SteamID> action)
        {
            if (_steamApiReady && _authorizedSteamIds.TryGetValue(playerSlot, out var steamId))
            {
                action(steamId);
                return;
            }

            if (!_pendingSteamActions.TryGetValue(playerSlot, out var actions))
            {
                actions = [];
                _pendingSteamActions[playerSlot] = actions;
            }

            actions.Add(action);
        }

        private void DrainPendingSteamActions(int playerSlot, SteamID steamId)
        {
            if (!_pendingSteamActions.TryGetValue(playerSlot, out var actions))
            {
                return;
            }

            _pendingSteamActions.Remove(playerSlot);
            foreach (var action in actions)
            {
                action(steamId);
            }
        }

        private void DrainAllPendingSteamActions()
        {
            foreach (var pair in _pendingSteamActions.ToList())
            {
                if (_authorizedSteamIds.TryGetValue(pair.Key, out var steamId))
                {
                    DrainPendingSteamActions(pair.Key, steamId);
                }
            }
        }
    }
}