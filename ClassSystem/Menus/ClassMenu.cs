using ClassSystem.Configuration;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Extensions;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using MenuManager;
using Microsoft.Extensions.Logging;

namespace ClassSystem.Menus;

public sealed class ClassMenu
{

    private static readonly Dictionary<string, ushort> KnifeDefinitions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = 42,
        ["knife"] = 42,
        ["weaponknife"] = 42,
        ["karambit"] = 507,
        ["knifekarambit"] = 507,
        ["m9bayonet"] = 508,
        ["knifem9bayonet"] = 508,
        ["bayonet"] = 500,
        ["flip"] = 505,
        ["knifeflip"] = 505,
        ["gut"] = 506,
        ["knifegut"] = 506,
        ["tactical"] = 509,
        ["knifetactical"] = 509,
        ["falchion"] = 512,
        ["knifefalchion"] = 512,
        ["survivalbowie"] = 514,
        ["bowie"] = 514,
        ["knifesurvivalbowie"] = 514,
        ["butterfly"] = 515,
        ["knifebutterfly"] = 515,
        ["shadowdaggers"] = 516,
        ["daggers"] = 516,
        ["skeleton"] = 525,
        ["knifeskeleton"] = 525
    };

    private static readonly Dictionary<string, string> KnifeEntityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default"] = "weapon_knife",
        ["knife"] = "weapon_knife",
        ["weaponknife"] = "weapon_knife",
        ["karambit"] = "weapon_knife_karambit",
        ["knifekarambit"] = "weapon_knife_karambit",
        ["m9bayonet"] = "weapon_knife_m9_bayonet",
        ["knifem9bayonet"] = "weapon_knife_m9_bayonet",
        ["bayonet"] = "weapon_bayonet",
        ["flip"] = "weapon_knife_flip",
        ["knifeflip"] = "weapon_knife_flip",
        ["gut"] = "weapon_knife_gut",
        ["knifegut"] = "weapon_knife_gut",
        ["tactical"] = "weapon_knife_tactical",
        ["knifetactical"] = "weapon_knife_tactical",
        ["falchion"] = "weapon_knife_falchion",
        ["knifefalchion"] = "weapon_knife_falchion",
        ["survivalbowie"] = "weapon_knife_survival_bowie",
        ["bowie"] = "weapon_knife_survival_bowie",
        ["knifesurvivalbowie"] = "weapon_knife_survival_bowie",
        ["butterfly"] = "weapon_knife_butterfly",
        ["knifebutterfly"] = "weapon_knife_butterfly",
        ["shadowdaggers"] = "weapon_knife_push",
        ["daggers"] = "weapon_knife_push",
        ["skeleton"] = "weapon_knife_skeleton",
        ["knifeskeleton"] = "weapon_knife_skeleton"
    };

    private IMenuApi? _api;
    private ILogger? _logger;

    private readonly Dictionary<int, string> _selectedClass = [];
    private readonly Dictionary<string, ClassDefinition> _classLookup = new(StringComparer.OrdinalIgnoreCase);
    private List<ClassDefinition> _classes = [];
    public IReadOnlyDictionary<int, string> GetSelections() => _selectedClass;
    public bool HasClass(string classId) => _classLookup.ContainsKey(classId);

    public event Action<CCSPlayerController, ClassDefinition>? ClassApplied;

    public bool TryGetSelectedClass(int userId, out ClassDefinition? info)
    {
        info = null;

        if (!_selectedClass.TryGetValue(userId, out var classId))
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

    public void SetClasses(IEnumerable<ClassDefinition> classes)
    {
        _classes = [.. classes.Where(cls => !string.IsNullOrWhiteSpace(cls.Id))];

        _classLookup.Clear();
        foreach (var cls in _classes)
        {
            _classLookup[cls.Id] = cls;
        }
    }
    //******************************** GETTERY *******************************

    public bool HasApi()
    {
        return _api != null;
    }

    public IMenuApi GetApi()
    {
        if (_api == null)
        {
            throw new InvalidOperationException("Menu API nie zostało ustawione.");
        }
        return _api;
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
        if (menu == null)
        {
            _logger.LogWarning("[DEBUG] Nie udało się utworzyć menu wyboru klas.");
            return;
        }

        var index = 0;


        // Dodaj opcje klas-tworzenie labela

        foreach (var cls in _classes)
        {
            var localCls = cls; // 🔑 KLUCZOWE
            string className = localCls.Name;
            index++;


            string label = $"{index}.{className}";

            menu.AddMenuOption(label, (p, option) =>
            {
                _api.CloseMenu(p);

                Server.NextFrame(() =>
                {
                    ApplyClass(p, localCls);
                });
            });
        }

        menu.AddMenuOption("Wyjdź", (p, option) =>
        {
            _api.CloseMenu(p);
        });

        menu.Open(player);
    }

    public bool TryApplyClass(CCSPlayerController player, string classId, out ClassDefinition? appliedInfo)
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
        if (!TryGetSelectedClass(player.UserId, out var info) || info == null)
        {
            return false;
        }

        Server.NextFrame(() => ApplyClassEffects(player, info, announce));
        return true;
    }

    public void ApplyClass(CCSPlayerController player, ClassDefinition info)
    {
        if (_logger == null) return;
        _logger.LogWarning("[DEBUG] ApplyClass");
        if (player == null || !player.IsValid)
            return;

        if (!player.UserId.HasValue)
        {
            _logger.LogWarning("[WARN] Brak UserId dla gracza {Player}. Nie można zapisać klasy.", player.PlayerName);
            return;
        }

        var userId = player.UserId.Value;
        _selectedClass[userId] = info.Id;

        Server.NextFrame(() =>
        {
            ApplyClassEffects(player, info, true);
            ClassApplied?.Invoke(player, info);
        });
    }

    private void ApplyClassEffects(CCSPlayerController player, ClassDefinition info, bool announce)
    {
        if (_logger == null) return;
        _logger.LogWarning("[DEBUG] ApplyClassEffects");
        if (player == null || !player.IsValid)
            return;

        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !player.PlayerPawn.IsValid)
        {
            _logger?.LogWarning("[DEBUG] Nie można zastosować klasy {ClassId} – pawn gracza jest niedostępny", info.Id);
            return;
        }

        var knifeDef = ResolveKnifeDefinition(info);
        var knifeEntityName = ResolveKnifeEntityName(info);

        _logger?.LogInformation("[FLOW-KNIFE] Class={ClassId}, KnifeConfig={KnifeConfig}, ResolvedDef={KnifeDef}, ResolvedEntity={KnifeEntity}", info.Id, info.Knife ?? "<null>", knifeDef?.ToString() ?? "<none>", knifeEntityName ?? "<none>");

        ApplyStats(pawn, info.Stats);
        GiveLoadout(player, info.Loadout);
        GiveArmorAndHelmetItem(player, info);


        /*if (knifeDef.HasValue)
        {
            _logger?.LogInformation("[FLOW-KNIFE] Scheduling knife apply for player={Player}, defIndex={DefIndex}, entity={KnifeEntity}", player.PlayerName, knifeDef.Value, knifeEntityName ?? "weapon_knife");
            Server.NextFrame(() =>
            {
                Server.NextFrame(() =>
                {
                    TryApplyKnife(player, knifeDef.Value, knifeEntityName ?? "weapon_knife");
                });
            });
        }
        else
        {
            _logger?.LogInformation("[FLOW-KNIFE] Knife not applied for class={ClassId} (no valid mapping).", info.Id);
        }*/
        //ApplySkills(player, info.Skills, announce);

        if (announce)
        {
            player.PrintToChat($"Wybrano klasę: {info.Name}");
            _logger?.LogInformation("[DEBUG] Gracz {Player} (UserId {UserId}) wybrał klasę {ClassId}", player.PlayerName, player.UserId, info.Id);
        }
    }

    private void ApplyStats(CCSPlayerPawn pawn, ClassStats stats)
    {
        if (_logger == null) return;
        _logger.LogWarning("[DEBUG] ApplyStats");
        pawn.MaxHealth = stats.Hp;
        pawn.Health = stats.Hp;
        pawn.VelocityModifier = stats.Speed;
    }

    private void GiveArmorAndHelmetItem(CCSPlayerController player, ClassDefinition classInfo)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        var itemName = classInfo.Helmet
            ? "item_assaultsuit"
            : classInfo.Armor > 0
                ? "item_kevlar"
                : string.Empty;

        if (string.IsNullOrWhiteSpace(itemName))
        {
            return;
        }

        try
        {
            player.GiveNamedItem(itemName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DEBUG] Nie udało się nadać itemu pancerza/hełmu {Item} graczowi {Player}.", itemName, player.PlayerName);
        }
    }

    private void TryApplyKnife(CCSPlayerController player, ushort defIndex, string knifeEntityName)
    {
        if (player == null || !player.IsValid)
        {
            _logger?.LogWarning("[FLOW-KNIFE] TryApplyKnife aborted: invalid player.");
            return;
        }

        TryApplyKnifeWithRetries(player, defIndex, knifeEntityName, 8);
    }

    private void TryApplyKnifeWithRetries(CCSPlayerController player, ushort defIndex, string knifeEntityName, int attemptsRemaining)
    {
        if (player == null || !player.IsValid || attemptsRemaining <= 0)
        {
            _logger?.LogWarning("[FLOW-KNIFE] Knife apply aborted for player={Player}, attemptsRemaining={Attempts}.", player?.PlayerName ?? "<null>", attemptsRemaining);
            return;
        }

        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !player.PlayerPawn.IsValid)
        {
            _logger?.LogWarning("[FLOW-KNIFE] TryApplyKnife aborted: invalid pawn for player={Player}.", player.PlayerName);
            return;
        }

        var knife = FindPlayerKnife(player);
        if (knife == null)
        {
            if (attemptsRemaining == 8)
            {
                _logger?.LogInformation("[FLOW-KNIFE] Giving preferred knife entity={KnifeEntity} to player={Player} before applying defIndex={DefIndex}.", knifeEntityName, player.PlayerName, defIndex);
                TryGiveKnifeEntity(player, knifeEntityName);
            }

            // Fallback: część serwerów akceptuje tylko bazowy weapon_knife.
            if (attemptsRemaining == 6 || attemptsRemaining == 3)
            {
                _logger?.LogInformation("[FLOW-KNIFE] Fallback give base knife for player={Player}, attemptsRemaining={Attempts}.", player.PlayerName, attemptsRemaining);
                TryGiveKnifeEntity(player, "weapon_knife");
            }

            Server.NextFrame(() => TryApplyKnifeWithRetries(player, defIndex, knifeEntityName, attemptsRemaining - 1));
            return;
        }

        if (!TryApplyKnifeEcon(knife, player, defIndex))
        {
            Server.NextFrame(() => TryApplyKnifeWithRetries(player, defIndex, knifeEntityName, attemptsRemaining - 1));
            return;
        }

        player.ExecuteClientCommandFromServer("slot3");
        Server.NextFrame(() => player.ExecuteClientCommandFromServer("slot3"));
    }

    private void TryGiveKnifeEntity(CCSPlayerController player, string knifeEntityName)
    {
        try
        {
            player.GiveNamedItem(knifeEntityName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FLOW-KNIFE] Failed to give knife entity={KnifeEntity} for player={Player}.", knifeEntityName, player.PlayerName);
        }
    }

    private CBasePlayerWeapon? FindPlayerKnife(CCSPlayerController player)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !player.PlayerPawn.IsValid)
        {
            return null;
        }

        var weaponServices = pawn.WeaponServices?.As<CCSPlayer_WeaponServices>();
        if (weaponServices == null)
        {
            return null;
        }

        foreach (var weaponHandle in weaponServices.MyWeapons)
        {
            var weapon = weaponHandle.Value;
            if (weapon == null || !weapon.IsValid)
            {
                continue;
            }

            var weaponName = weapon.GetWeaponName();
            if (weaponName.Contains("knife", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(weaponName, "weapon_bayonet", StringComparison.OrdinalIgnoreCase))
            {
                return weapon;
            }
        }

        return null;
    }

    private bool TryApplyKnifeEcon(CBasePlayerWeapon knife, CCSPlayerController player, ushort defIndex)
    {
        try
        {
            var econ = knife.As<CEconEntity>();
            if (econ == null || !econ.IsValid)
            {
                return false;
            }

            var itemView = econ.AttributeManager.Item;
            itemView.ItemDefinitionIndex = defIndex;

            Utilities.SetStateChanged(knife, "CEconItemView", "m_iItemDefinitionIndex");
            Utilities.SetStateChanged(knife, "CEconEntity", "m_AttributeManager");

            _logger?.LogInformation("[FLOW-KNIFE] Applied ItemDefinitionIndex={DefIndex} to knife for player={Player}.", defIndex, player.PlayerName);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[FLOW-KNIFE] Failed applying knife econ for player={Player}, defIndex={DefIndex}. If FollowCS2ServerGuidelines is enabled, only safe props can be written.", player.PlayerName, defIndex);
            return false;
        }
    }

    private void ApplySkills(CCSPlayerController player, IReadOnlyCollection<Configuration.SkillDefinition> skills, bool announce)
    {
        if (skills.Count == 0)
        {
            return;
        }
        var skillIds = skills.Select(skill => skill.Id).ToArray();

        if (announce)
        {
            var skillsText = string.Join(", ", skillIds);
            player.PrintToChat($"Umiejętności klasy: {skillsText}");
        }

        _logger?.LogInformation("[DEBUG] Zastosowano umiejętności {Skills} dla gracza {Player}", string.Join(", ", skillIds), player.PlayerName);
    }

    private void GiveLoadout(CCSPlayerController player, IReadOnlyCollection<string> loadout)
    {
        if (loadout.Count == 0)
        {
            return;
        }

        var normalizedLoadout = loadout
            .Select(NormalizeWeaponName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Where(name => !string.Equals(name, "weapon_c4", StringComparison.OrdinalIgnoreCase))
            .Where(name => !string.Equals(name, "weapon_knife", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (normalizedLoadout.Count == 0)
        {
            return;
        }

        Server.NextFrame(() => StartLoadoutApplication(player, normalizedLoadout));
    }

    private ushort? ResolveKnifeDefinition(ClassDefinition info)
    {
        if (string.IsNullOrWhiteSpace(info.Knife))
        {
            return null;
        }

        if (TryParseKnifeDefinition(info.Knife, out var knifeDef))
        {
            _logger?.LogInformation("[FLOW-KNIFE] Resolved knife '{KnifeName}' -> defIndex={DefIndex} for class={ClassId}.", info.Knife, knifeDef, info.Id);
            return knifeDef;
        }

        _logger?.LogWarning("[WARN] Klasa {ClassId} ma nieprawidłowy knife '{KnifeName}'.", info.Id, info.Knife);
        return null;
    }

    private string? ResolveKnifeEntityName(ClassDefinition info)
    {
        if (string.IsNullOrWhiteSpace(info.Knife))
        {
            return null;
        }

        var lowered = info.Knife.Trim().ToLowerInvariant();
        if (KnifeEntityNames.TryGetValue(lowered, out var entityName))
        {
            return entityName;
        }

        if (lowered.StartsWith("weapon_", StringComparison.Ordinal))
        {
            lowered = lowered["weapon_".Length..];
        }

        return KnifeEntityNames.TryGetValue(lowered, out entityName) ? entityName : null;
    }

    private static bool TryParseKnifeDefinition(string knifeName, out ushort defIndex)
    {
        var lowered = knifeName.Trim().ToLowerInvariant();

        if (KnifeDefinitions.TryGetValue(lowered, out defIndex))
        {
            return true;
        }

        if (lowered.StartsWith("weapon_", StringComparison.Ordinal))
        {
            lowered = lowered["weapon_".Length..];
        }

        if (KnifeDefinitions.TryGetValue(lowered, out defIndex))
        {
            return true;
        }

        return false;
    }

    private void StartLoadoutApplication(CCSPlayerController player, List<string> normalizedLoadout)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        StartLoadoutApplicationInternal(player, normalizedLoadout);
    }

    private void StartLoadoutApplicationInternal(CCSPlayerController player, List<string> normalizedLoadout)
    {
        if (player == null || !player.IsValid)
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

        _logger?.LogInformation(
            "[DEBUG] Zastosowano loadout ({ItemCount} itemy) dla gracza {Player}",
            normalizedLoadout.Count,
            player.PlayerName
        );

        var failedItems = new List<string>();
        GiveLoadoutItem(player, normalizedLoadout, 0, failedItems);
    }

    private void GiveLoadoutItem(CCSPlayerController player, List<string> normalizedLoadout, int index, List<string> failedItems)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        if (index >= normalizedLoadout.Count)
        {
            if (failedItems.Count > 0)
            {
                _logger?.LogWarning(
                    "[DEBUG] Nie udało się nadać {FailedCount} itemów dla gracza {Player}: {FailedItems}",
                    failedItems.Count,
                    player.PlayerName,
                    string.Join(", ", failedItems)
                );
            }



            return;
        }

        var itemName = normalizedLoadout[index];
        try
        {
            player.GiveNamedItem(itemName);
        }
        catch (Exception ex)
        {
            failedItems.Add(itemName);
            _logger?.LogWarning(ex, "[DEBUG] Nie udało się nadać {Weapon} graczowi {Player}", itemName, player.PlayerName);
        }

        Server.NextFrame(() => GiveLoadoutItem(player, normalizedLoadout, index + 1, failedItems));
    }

    private string NormalizeWeaponName(string weaponName)
    {
        if (_logger == null) return string.Empty;
        _logger.LogWarning($"[DEBUG] NormalizeWeaponName {weaponName}");
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

    internal bool TryGetSelectedClass(int? userId, out ClassDefinition classInfo)
    {
        classInfo = null!;
        if (!userId.HasValue)
        {
            return false;
        }

        return TryGetSelectedClass(userId.Value, out classInfo);
    }
}
