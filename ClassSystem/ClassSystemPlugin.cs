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
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Utils;
using MenuManager;
using Microsoft.Extensions.Logging;
using static System.Net.Mime.MediaTypeNames;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        private readonly Dictionary<int, PlayerState> _players = [];
        private readonly Dictionary<ulong, CsTeam> _pendingAutoTeam = []; //autobalans
        private bool _restartDoneForLowPlayers = false;
        private bool _classSelectionOpen;
        private const int FreezeTimeSeconds = 20;
        private const float ClassSelectionWindowSeconds = FreezeTimeSeconds;
        private bool _restartAllowed = true;
        private int _classSelectionToken = 0;
        private readonly HashSet<string> _steamIdWarnings = [];
        private bool _steamApiReady;


        // === Skill constants ===
        private const string DefaultClassId = "newbie";

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

            // -----------Rejestracja listenerów i eventów.

            //FLOW: ważne eventy na górze, mniej ważne niżej
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
            RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn);
            RegisterEventHandler<EventRoundFreezeEnd>(OnEventFreezeEnd);
            RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnect);
            RegisterListener<Listeners.OnGameServerSteamAPIActivated>(OnSteamApiActivated);
            RegisterListener<Listeners.OnGameServerSteamAPIDeactivated>(OnSteamApiDeactivated);

            //EVENTY ZWIĄZANE Z GRĄ / ROZGRYWKĄ
            RegisterListener<Listeners.OnPlayerTakeDamagePre>(OnPlayerTakeDamagePre);
            AddCommandListener("jointeam", OnJoinTeam, HookMode.Pre);
            AddCommandListener("drop", OnDropCommand);


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
            var state = GetOrCreatePlayerState(playerSlot);
            state.SetSteamId(steamId);
            state.DrainPendingSteamActions();

            if (!state.IsRegisteredInSession)
            {
                state.IsRegisteredInSession = true;
                Logger.LogInformation($"[INFO] Zarejestrowano gracza: {steamId}");

            }
        }

        public void OnClientPutInServer(int playerSlot)
        {

            var player = Utilities.GetPlayerFromSlot(playerSlot);
            if (player == null || !player.IsValid || player.IsBot)
                return;

            var state = GetOrCreatePlayerState(playerSlot);
            state.SetUserId(player.UserId);

            Logger.LogInformation($"[INFO] Gracz dołączył do serwera {player.PlayerName} (slot={playerSlot})");

            player.PrintToChat($"Witaj, {player.PlayerName}!");
            EnsureDefaultClass(player);
            player.PrintToChat($"Wybierz klasę, komend !klasa ");

            AddTimer(0.2f, () =>
            {
                if (!player.IsValid || player.IsBot)
                    return;

                if (player.Team is CsTeam.CounterTerrorist or CsTeam.Terrorist)
                    return;

                EnsureBalancedTeam(player);
                RestartIfNeeded();
            });

            var steamId = SteamIdSafe(player, nameof(OnClientPutInServer));
            if (steamId != null)
            {
                state.SetSteamId(steamId);
            }
            else
            {
                EnqueueSteamAction(playerSlot, id =>
                {
                    var pendingState = GetOrCreatePlayerState(playerSlot);
                    pendingState.SetSteamId(id);
                });
            }
        }

        public void OnMapStart(string mapName)
        {
            Logger.LogInformation("[DEBUG] OnmapStart funkcja");
            AddTimer(0.5f, () =>
            {
                Server.ExecuteCommand("exec conVar_codmod.cfg");
                Server.ExecuteCommand("mp_restartgame 1");

                Logger.LogInformation("[FLOW] Załadowanie convar");
            });
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

            var player = ev.Userid;
            if (player == null || !player.IsValid)
            {
                return HookResult.Continue;
            }

            AddTimer(0.1f, () =>
            {
                if (player == null || !player.IsValid)
                {
                    return;
                }

                _classMenu.ApplySavedClass(player);
            });

            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart ev, GameEventInfo info)
        {
            Logger.LogInformation("[DEBUG] Runda rozpoczęta - OnRoundStart");
            // Okno wyboru klas tylko na starcie rundy.
            _classSelectionOpen = true;

            foreach (var state in _players.Values)
            {
                state.ResetRound();
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
            if (!_players.TryGetValue(playerSlot, out var state))
            {
                return;
            }

            _players.Remove(playerSlot);
            // 🔑 KLUCZOWE: pozwól na restart przy następnym wejściu
            _restartAllowed = true;

            Logger.LogInformation(
                $"[DEBUG] Player {state.SteamId64} left (slot {playerSlot})"
            );
        }

        private void EnsureDefaultClass(CCSPlayerController player)
        {
            if (!player.UserId.HasValue)
            {
                return;
            }

            if (_classMenu.TryGetSelectedClass(player.UserId.Value, out _))
            {
                return;
            }

            if (!_classMenu.TryApplyClass(player, DefaultClassId, out var classInfo) || classInfo == null)
            {
                Logger.LogWarning("[WARN] Nie udało się przypisać domyślnej klasy '{ClassId}' graczowi {Player}", DefaultClassId, player.PlayerName);
                return;
            }

            var state = GetOrCreatePlayerState(player.Slot);
            state.SelectedClassId = classInfo.Id;
            state.SelectedClassThisRound = false;
            player.PrintToChat($"Przypisano domyślną klasę: {classInfo.Name}");
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

            var state = GetOrCreatePlayerState(player.Slot);
            state.SetUserId(player.UserId);
            state.SelectedClassThisRound = true;
            state.SelectedClassId = info.Id;

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
            state.RuntimeClass = runtimeClass;

            Logger.LogInformation(
                "[DEBUG] Przypisano klasę '{ClassId}' graczowi {Player} ({SkillCount} skilli)",
                classId,
                player.PlayerName,
                runtimeSkills.Count
            );
        }


        // === Balans drużyn / reset ===
        private void EnsureBalancedTeam(CCSPlayerController player)
        {
            var players = Utilities.GetPlayers()
                .Where(p => p != null && p.IsValid && !p.IsBot && p != player);

            var ctCount = players.Count(p => p.Team == CsTeam.CounterTerrorist);
            var ttCount = players.Count(p => p.Team == CsTeam.Terrorist);

            var desiredTeam = ttCount > ctCount
                ? CsTeam.CounterTerrorist
                : CsTeam.Terrorist;

            if (player.Team == desiredTeam)
                return;

            Logger.LogInformation(
                "[INFO] Zmieniam drużynę gracza {PlayerName} na {DesiredTeam}",
                player.PlayerName,
                desiredTeam
            );

            player.ChangeTeam(desiredTeam);
        }

        private void RestartIfNeeded()
        {
            var count = Utilities.GetPlayers()
                .Count(p => p != null &&
                            p.IsValid &&
                            !p.IsBot &&
                            (p.Team == CsTeam.CounterTerrorist || p.Team == CsTeam.Terrorist));

            if (count is 1 or 2)
                _restartAllowed = true;

            if ((count is 1 or 2) && _restartAllowed)
            {
                _restartAllowed = false;

                Logger.LogInformation("[FLOW] Restarting game for {PlayerCount} players", count);
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

            if (!player.UserId.HasValue)
            {
                player.PrintToChat("Brak UserId - spróbuj ponownie za chwilę.");
                return false;
            }

            if (!TryGetPlayerState(player, out var state))
            {
                player.PrintToChat("Nie udało się odczytać stanu gracza - spróbuj ponownie.");
                return false;
            }

            if (state.SelectedClassThisRound)
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
        [ConsoleCommand("css_test2", "TEST: daje weapon_knife, potem daje weapon_knife_karambit i ustawia def=507 na karambicie")]
        public void CommandTestKarambit2(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.IsBot)
                return;

            Logger.LogInformation("[TEST-KNIFE2] start player={Player}", player.PlayerName);

            try
            {
                player.GiveNamedItem("weapon_knife");
                Logger.LogInformation("[TEST-KNIFE2] given base weapon_knife player={Player}", player.PlayerName);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[TEST-KNIFE2] failed give weapon_knife player={Player}", player.PlayerName);
                return;
            }

            Server.NextFrame(() =>
            {
                try
                {
                    // 2) spróbuj dać encję karambita
                    player.GiveNamedItem("weapon_knife_karambit");
                    Logger.LogInformation("[TEST-KNIFE2] given entity weapon_knife_karambit player={Player}", player.PlayerName);
                }
                catch (Exception ex2)
                {
                    Logger.LogWarning(ex2, "[TEST-KNIFE2] failed give weapon_knife_karambit player={Player}", player.PlayerName);
                }

                // 3) w kolejnej klatce znajdź karambita i ustaw mu def=507
                Server.NextFrame(() =>
                {
                    try
                    {
                        var pawn = player.PlayerPawn.Value;
                        if (pawn == null || !player.PlayerPawn.IsValid)
                        {
                            Logger.LogWarning("[TEST-KNIFE2] pawn invalid player={Player}", player.PlayerName);
                            return;
                        }

                        var ws = pawn.WeaponServices?.As<CCSPlayer_WeaponServices>();
                        if (ws == null)
                        {
                            Logger.LogWarning("[TEST-KNIFE2] weaponServices null player={Player}", player.PlayerName);
                            return;
                        }

                        CBasePlayerWeapon? karambit = null;

                        // dump przed + szukanie karambita
                        foreach (var h in ws.MyWeapons)
                        {
                            var w = h.Value;
                            if (w == null || !w.IsValid) continue;

                            ushort def = 0;
                            try
                            {
                                var econ = w.As<CEconEntity>();
                                if (econ != null && econ.IsValid)
                                    def = econ.AttributeManager.Item.ItemDefinitionIndex;
                            }
                            catch { }

                            var name = w.GetWeaponName();
                            Logger.LogInformation("[TEST-KNIFE2] dump(before_apply) idx={Idx} name={Name} def={Def}", w.Index, name, def);

                            if (string.Equals(name, "weapon_knife_karambit", StringComparison.OrdinalIgnoreCase))
                                karambit = w;
                        }

                        if (karambit == null)
                        {
                            Logger.LogWarning("[TEST-KNIFE2] NO weapon_knife_karambit found in inventory player={Player}", player.PlayerName);
                            player.PrintToChat("[TEST] Nie znalazłem weapon_knife_karambit w ekwipunku po GiveNamedItem.");
                            return;
                        }

                        var econEntity = karambit.As<CEconEntity>();
                        if (econEntity == null || !econEntity.IsValid)
                        {
                            Logger.LogWarning("[TEST-KNIFE2] karambit has no CEconEntity idx={Idx}", karambit.Index);
                            return;
                        }

                        econEntity.AttributeManager.Item.ItemDefinitionIndex = 507;
                        Utilities.SetStateChanged(karambit, "CEconItemView", "m_iItemDefinitionIndex");
                        Utilities.SetStateChanged(karambit, "CEconEntity", "m_AttributeManager");

                        Logger.LogInformation("[TEST-KNIFE2] applied def=507 on KARMBIT idx={Idx} player={Player}", karambit.Index, player.PlayerName);

                        // 4) wymuś equip
                        player.ExecuteClientCommandFromServer("slot2");
                        player.ExecuteClientCommandFromServer("slot3");

                        Server.NextFrame(() =>
                        {
                            player.ExecuteClientCommandFromServer("slot3");

                            // dump po
                            foreach (var h2 in ws.MyWeapons)
                            {
                                var w2 = h2.Value;
                                if (w2 == null || !w2.IsValid) continue;

                                ushort def2 = 0;
                                try
                                {
                                    var econ2 = w2.As<CEconEntity>();
                                    if (econ2 != null && econ2.IsValid)
                                        def2 = econ2.AttributeManager.Item.ItemDefinitionIndex;
                                }
                                catch { }

                                Logger.LogInformation("[TEST-KNIFE2] dump(after_apply) idx={Idx} name={Name} def={Def}",
                                    w2.Index, w2.GetWeaponName(), def2);
                            }
                        });

                        player.PrintToChat("[TEST] Karambit entity + def=507 próba wykonana.");
                    }
                    catch (Exception ex3)
                    {
                        Logger.LogWarning(ex3, "[TEST-KNIFE2] failed apply flow player={Player}", player.PlayerName);
                    }
                });
            });
        }


        [ConsoleCommand("css_test", "TEST: daje bazowy weapon_knife i próbuje zmienić go econem na karambit (def=507)")]
        public void CommandTestKarambit(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid || player.IsBot)
                return;

            Logger.LogInformation("[TEST-KNIFE] start player={Player}", player.PlayerName);

            try
            {
                // 1) Daj bazowy nóż (nic nie usuwamy)
                player.GiveNamedItem("weapon_knife");
                Logger.LogInformation("[TEST-KNIFE] given weapon_knife to player={Player}", player.PlayerName);

                // 2) Dopiero w następnej klatce grzebiemy w ekwipunku / econ
                Server.NextFrame(() =>
                {
                    try
                    {
                        var pawn = player.PlayerPawn.Value;
                        if (pawn == null || !player.PlayerPawn.IsValid)
                        {
                            Logger.LogWarning("[TEST-KNIFE] pawn invalid player={Player}", player.PlayerName);
                            return;
                        }

                        var weaponServices = pawn.WeaponServices?.As<CCSPlayer_WeaponServices>();
                        if (weaponServices == null)
                        {
                            Logger.LogWarning("[TEST-KNIFE] weaponServices null player={Player}", player.PlayerName);
                            return;
                        }

                        // dump przed
                        foreach (var h in weaponServices.MyWeapons)
                        {
                            var w = h.Value;
                            if (w == null || !w.IsValid) continue;

                            ushort def = 0;
                            try
                            {
                                var econ = w.As<CEconEntity>();
                                if (econ != null && econ.IsValid)
                                    def = econ.AttributeManager.Item.ItemDefinitionIndex;
                            }
                            catch { /* ignore */ }

                            Logger.LogInformation("[TEST-KNIFE] dump(before_apply) idx={Idx} name={Name} def={Def}",
                                w.Index, w.GetWeaponName(), def);
                        }

                        // 3) znajdź bazowy weapon_knife
                        CBasePlayerWeapon? knife = null;
                        foreach (var h in weaponServices.MyWeapons)
                        {
                            var w = h.Value;
                            if (w == null || !w.IsValid) continue;

                            if (string.Equals(w.GetWeaponName(), "weapon_knife", StringComparison.OrdinalIgnoreCase))
                            {
                                knife = w;
                                break;
                            }
                        }

                        if (knife == null)
                        {
                            Logger.LogWarning("[TEST-KNIFE] weapon_knife not found in inventory player={Player}", player.PlayerName);
                            player.PrintToChat("[TEST] Nie znaleziono weapon_knife w ekwipunku.");
                            return;
                        }

                        Logger.LogInformation("[TEST-KNIFE] found knife idx={Idx} name={Name}", knife.Index, knife.GetWeaponName());

                        // 4) ECON: ustaw defindex na karambit (507)
                        var econEntity = knife.As<CEconEntity>();
                        if (econEntity == null || !econEntity.IsValid)
                        {
                            Logger.LogWarning("[TEST-KNIFE] CEconEntity invalid for knife idx={Idx}", knife.Index);
                            player.PrintToChat("[TEST] Knife nie ma CEconEntity.");
                            return;
                        }

                        econEntity.AttributeManager.Item.ItemDefinitionIndex = 507;

                        Utilities.SetStateChanged(knife, "CEconItemView", "m_iItemDefinitionIndex");
                        Utilities.SetStateChanged(knife, "CEconEntity", "m_AttributeManager");

                        Logger.LogInformation("[TEST-KNIFE] applied def=507 on idx={Idx} player={Player}", knife.Index, player.PlayerName);

                        // 5) wymuś re-equip (żeby klient to zobaczył)
                        // slot2 -> slot3 -> slot2 -> slot3 (po klatkach)
                        player.ExecuteClientCommandFromServer("slot2");
                        player.ExecuteClientCommandFromServer("slot3");

                        Server.NextFrame(() =>
                        {
                            player.ExecuteClientCommandFromServer("slot2");
                            player.ExecuteClientCommandFromServer("slot3");

                            // dump po
                            try
                            {
                                foreach (var h2 in weaponServices.MyWeapons)
                                {
                                    var w2 = h2.Value;
                                    if (w2 == null || !w2.IsValid) continue;

                                    ushort def2 = 0;
                                    try
                                    {
                                        var econ2 = w2.As<CEconEntity>();
                                        if (econ2 != null && econ2.IsValid)
                                            def2 = econ2.AttributeManager.Item.ItemDefinitionIndex;
                                    }
                                    catch { /* ignore */ }

                                    Logger.LogInformation("[TEST-KNIFE] dump(after_apply) idx={Idx} name={Name} def={Def}",
                                        w2.Index, w2.GetWeaponName(), def2);
                                }
                            }
                            catch { /* ignore */ }
                        });

                        player.PrintToChat("[TEST] Próba ustawienia karambita (def=507) wykonana. Sprawdź model noża.");
                    }
                    catch (Exception ex2)
                    {
                        Logger.LogWarning(ex2, "[TEST-KNIFE] failed in NextFrame player={Player}", player.PlayerName);
                        player.PrintToChat("[TEST] Błąd w fazie apply.");
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "[TEST-KNIFE] initial give failed player={Player}", player.PlayerName);
                player.PrintToChat("[TEST] Nie udało się dać weapon_knife.");
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
            var state = GetOrCreatePlayerState(playerSlot);
            state.EnqueueSteamAction(action);
        }

        private void DrainPendingSteamActions(int playerSlot, SteamID steamId)
        {
            var state = GetOrCreatePlayerState(playerSlot);
            state.SetSteamId(steamId);
            state.DrainPendingSteamActions();
        }

        private void DrainAllPendingSteamActions()
        {
            foreach (var state in _players.Values)
            {
                state.DrainPendingSteamActions();
            }
        }

        private PlayerState GetOrCreatePlayerState(int playerSlot)
        {
            if (_players.TryGetValue(playerSlot, out var state))
            {
                return state;
            }

            state = new PlayerState(playerSlot);
            _players[playerSlot] = state;
            return state;
        }

        private bool TryGetPlayerState(CCSPlayerController player, out PlayerState state)
        {
            if (!_players.TryGetValue(player.Slot, out state!))
            {
                return false;
            }

            state.SetUserId(player.UserId);
            return true;
        }

        private HookResult OnDropCommand(CCSPlayerController? player, CommandInfo info)
        {
            if (player == null || !player.IsValid)
                return HookResult.Continue;

            // blokuj drop
            player.PrintToChat("Drop broni jest zablokowany.");
            return HookResult.Stop;
        }
    }
}