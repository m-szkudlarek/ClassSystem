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
    private static readonly Dictionary<string, string> WeaponAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["knife"] = "weapon_knife",
        ["karambit"] = "weapon_knife",
        ["knifekarambit"] = "weapon_knife",
        ["knifebutterfly"] = "weapon_knife",
        ["butterfly"] = "weapon_knife",
        ["knifeflip"] = "weapon_knife",
        ["flip"] = "weapon_knife",
        ["knifem9bayonet"] = "weapon_knife",
        ["m9bayonet"] = "weapon_knife",
        ["bayonet"] = "weapon_knife",
        ["knifeskeleton"] = "weapon_knife",
        ["skeleton"] = "weapon_knife",
        ["knifetactical"] = "weapon_knife",
        ["tactical"] = "weapon_knife",
        ["knifesurvivalbowie"] = "weapon_knife",
        ["survivalbowie"] = "weapon_knife"
    };

    private static readonly Dictionary<string, ushort> KnifeDefinitionIndexes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["knife"] = 42,
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


    private static readonly Dictionary<ushort, string> KnifeEntityByDefinition = new()
    {
        [42] = "weapon_knife",
        [500] = "weapon_bayonet",
        [505] = "weapon_knife_flip",
        [506] = "weapon_knife_gut",
        [507] = "weapon_knife_karambit",
        [508] = "weapon_knife_m9_bayonet",
        [509] = "weapon_knife_tactical",
        [512] = "weapon_knife_falchion",
        [514] = "weapon_knife_survival_bowie",
        [515] = "weapon_knife_butterfly",
        [516] = "weapon_knife_push",
        [525] = "weapon_knife_skeleton"
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

        ApplyStats(pawn, info.Stats);

        ushort? knifeDefinitionIndex = null;
        if (TryGetKnifeDefinitionFromLoadout(info.Loadout, out var parsedKnifeDefinitionIndex))
        {
            knifeDefinitionIndex = parsedKnifeDefinitionIndex;
        }

        GiveLoadout(player, info.Loadout, knifeDefinitionIndex);
        GiveArmorAndHelmetItem(player, info);
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

    private void TryApplyKnifeWithRetries(CCSPlayerController player, ushort itemDefinitionIndex, int attemptsRemaining)
    {
        if (player == null || !player.IsValid || attemptsRemaining <= 0)
        {
            _logger?.LogWarning("[KNIFE_TRACE] Przerwano retry noża. playerValid={IsValid}, attemptsRemaining={Attempts}", player?.IsValid, attemptsRemaining);
            return;
        }

        _logger?.LogInformation("[KNIFE_TRACE] Próba ustawienia noża dla {Player}. attemptsRemaining={Attempts}, def={DefinitionIndex}", player.PlayerName, attemptsRemaining, itemDefinitionIndex);

        var preferredKnifeEntityName = GetKnifeEntityName(itemDefinitionIndex);
        if (attemptsRemaining == 6 && !string.IsNullOrWhiteSpace(preferredKnifeEntityName) && !string.Equals(preferredKnifeEntityName, "weapon_knife", StringComparison.OrdinalIgnoreCase))
        {
            TryGivePreferredKnifeEntity(player, preferredKnifeEntityName);
        }

        var knife = FindPlayerKnife(player, preferredKnifeEntityName);

        if (knife == null)
        {
            _logger?.LogWarning("[KNIFE_TRACE] Nie znaleziono noża u gracza {Player}. attemptsRemaining={Attempts}", player.PlayerName, attemptsRemaining);
            if (attemptsRemaining == 6)
            {
                try
                {
                    _logger?.LogInformation("[KNIFE_TRACE] Nadaję bazowy weapon_knife graczowi {Player}", player.PlayerName);
                    player.GiveNamedItem("weapon_knife");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[DEBUG] Nie udało się nadać bazowego noża graczowi {Player}", player.PlayerName);
                }
            }

            Server.NextFrame(() => TryApplyKnifeWithRetries(player, itemDefinitionIndex, attemptsRemaining - 1));
            return;
        }

        if (!TryApplyKnifeEcon(knife, player, itemDefinitionIndex))
        {
            _logger?.LogWarning("[KNIFE_TRACE] TryApplyKnifeEcon zwrócił false dla {Player}. Przerywam dalsze próby.", player.PlayerName);
            return;
        }

        RemoveExtraKnives(player, knife);

        _logger?.LogInformation(
            "[KNIFE] Gracz {Player} dostał nóż ItemDefinitionIndex={DefinitionIndex}.",
            player.PlayerName,
            itemDefinitionIndex
        );
    }

    private CBasePlayerWeapon? FindPlayerKnife(CCSPlayerController player, string? preferredKnifeEntityName = null)
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

        CBasePlayerWeapon? fallbackKnife = null;

        foreach (var weaponHandle in weaponServices.MyWeapons)
        {
            var weapon = weaponHandle.Value;
            if (weapon == null || !weapon.IsValid)
            {
                continue;
            }

            var weaponName = weapon.GetWeaponName();
            if (!weaponName.Contains("knife", StringComparison.OrdinalIgnoreCase) && !string.Equals(weaponName, "weapon_bayonet", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(preferredKnifeEntityName) && string.Equals(weaponName, preferredKnifeEntityName, StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInformation("[KNIFE_TRACE] Znaleziono preferowany nóż encji {WeaponName} (index {WeaponIndex}) dla gracza {Player}", weaponName, weapon.Index, player.PlayerName);
                return weapon;
            }

            fallbackKnife ??= weapon;
        }

        if (fallbackKnife != null)
        {
            _logger?.LogInformation("[KNIFE_TRACE] Znaleziono fallback nóż encji {WeaponName} (index {WeaponIndex}) dla gracza {Player}", fallbackKnife.GetWeaponName(), fallbackKnife.Index, player.PlayerName);
        }

        return fallbackKnife;
    }


    private string GetKnifeEntityName(ushort itemDefinitionIndex)
    {
        return KnifeEntityByDefinition.TryGetValue(itemDefinitionIndex, out var entityName)
            ? entityName
            : "weapon_knife";
    }

    private void TryGivePreferredKnifeEntity(CCSPlayerController player, string preferredKnifeEntityName)
    {
        try
        {
            _logger?.LogInformation("[KNIFE_TRACE] Próbuję nadać preferowaną encję noża {WeaponName} graczowi {Player}", preferredKnifeEntityName, player.PlayerName);
            player.GiveNamedItem(preferredKnifeEntityName);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[KNIFE_TRACE] Nie udało się nadać preferowanej encji noża {WeaponName} graczowi {Player}", preferredKnifeEntityName, player.PlayerName);
        }
    }

    private bool TryApplyKnifeEcon(CBasePlayerWeapon knife, CCSPlayerController player, ushort itemDefinitionIndex)
    {
        try
        {
            var econEntity = knife.As<CEconEntity>();
            if (econEntity == null || !econEntity.IsValid)
            {
                return false;
            }

            var itemView = econEntity.AttributeManager.Item;
            _logger?.LogInformation("[KNIFE_TRACE] Ustawiam ItemDefinitionIndex={DefinitionIndex} na weapon index={WeaponIndex} dla {Player}", itemDefinitionIndex, knife.Index, player.PlayerName);
            itemView.ItemDefinitionIndex = itemDefinitionIndex;

            Utilities.SetStateChanged(knife, "CEconItemView", "m_iItemDefinitionIndex");
            Utilities.SetStateChanged(knife, "CEconEntity", "m_AttributeManager");

            _logger?.LogInformation(
                "[KNIFE] Ustawiono ItemDefinitionIndex={DefinitionIndex} dla gracza {Player} i wywołano StateChanged.",
                itemDefinitionIndex,
                player.PlayerName
            );

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[KNIFE] Nie udało się ustawić dozwolonego econ noża dla gracza {Player}", player.PlayerName);
            return false;
        }
    }

    private void RemoveExtraKnives(CCSPlayerController player, CBasePlayerWeapon keepKnife)
    {
        var pawn = player.PlayerPawn.Value;
        if (pawn == null || !player.PlayerPawn.IsValid)
        {
            return;
        }

        var weaponServices = pawn.WeaponServices?.As<CCSPlayer_WeaponServices>();
        if (weaponServices == null)
        {
            return;
        }

        foreach (var weaponHandle in weaponServices.MyWeapons)
        {
            var weapon = weaponHandle.Value;
            if (weapon == null || !weapon.IsValid || weapon.Index == keepKnife.Index)
            {
                continue;
            }

            var weaponName = weapon.GetWeaponName();
            if (!weaponName.Contains("knife", StringComparison.OrdinalIgnoreCase) && !string.Equals(weaponName, "weapon_bayonet", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                pawn.RemovePlayerItem(weapon);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[KNIFE] Nie udało się usunąć nadmiarowego noża gracza {Player}", player.PlayerName);
            }
        }
    }

    private bool TryGetKnifeDefinitionFromLoadout(IReadOnlyCollection<string> loadout, out ushort itemDefinitionIndex)
    {
        itemDefinitionIndex = 0;

        foreach (var rawItem in loadout)
        {
            if (TryGetKnifeDefinitionIndex(rawItem, out itemDefinitionIndex))
            {
                _logger?.LogInformation("[KNIFE_TRACE] Rozpoznano nóż z loadoutu: {RawItem} -> def={DefinitionIndex}", rawItem, itemDefinitionIndex);
                return true;
            }

            _logger?.LogInformation("[KNIFE_TRACE] Pozycja loadoutu nie jest nożem lub brak mapowania: {RawItem}", rawItem);
        }

        return false;
    }

    private bool TryGetKnifeDefinitionIndex(string? weaponName, out ushort itemDefinitionIndex)
    {
        itemDefinitionIndex = 0;

        if (string.IsNullOrWhiteSpace(weaponName))
        {
            _logger?.LogInformation("[KNIFE_TRACE] Pusta nazwa broni w TryGetKnifeDefinitionIndex");
            return false;
        }

        var compactName = weaponName.Trim()
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToLowerInvariant();

        if (compactName.StartsWith("weapon", StringComparison.Ordinal))
        {
            compactName = compactName[6..];
        }

        if (compactName.EndsWith("ag2", StringComparison.Ordinal))
        {
            compactName = compactName[..^3];
        }

        var result = KnifeDefinitionIndexes.TryGetValue(compactName, out itemDefinitionIndex);
        _logger?.LogInformation("[KNIFE_TRACE] TryGetKnifeDefinitionIndex input={Input} compact={Compact} result={Result} def={DefinitionIndex}", weaponName, compactName, result, itemDefinitionIndex);
        return result;
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

    private void GiveLoadout(CCSPlayerController player, IReadOnlyCollection<string> loadout, ushort? knifeDefinitionIndex)
    {
        if (loadout.Count == 0)
        {
            return;
        }

        var normalizedLoadout = loadout
            .Select(NormalizeWeaponName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (normalizedLoadout.Count == 0)
        {
            return;
        }

        Server.NextFrame(() => StartLoadoutApplication(player, normalizedLoadout, knifeDefinitionIndex));
    }

    private void StartLoadoutApplication(CCSPlayerController player, List<string> normalizedLoadout, ushort? knifeDefinitionIndex)
    {
        if (player == null || !player.IsValid)
        {
            return;
        }

        if (player.Team == CsTeam.Terrorist && PlayerHasBomb(player))
        {
            TryDropBomb(player, () => StartLoadoutApplicationInternal(player, normalizedLoadout, knifeDefinitionIndex));
            return;
        }

        StartLoadoutApplicationInternal(player, normalizedLoadout, knifeDefinitionIndex);
    }

    private void StartLoadoutApplicationInternal(CCSPlayerController player, List<string> normalizedLoadout, ushort? knifeDefinitionIndex)
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
        GiveLoadoutItem(player, normalizedLoadout, 0, failedItems, knifeDefinitionIndex);
    }

    private void GiveLoadoutItem(CCSPlayerController player, List<string> normalizedLoadout, int index, List<string> failedItems, ushort? knifeDefinitionIndex)
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

            if (knifeDefinitionIndex.HasValue)
            {
                _logger?.LogInformation("[KNIFE_TRACE] Loadout zakończony, aplikuję econ noża def={DefinitionIndex} dla {Player}", knifeDefinitionIndex.Value, player.PlayerName);
                Server.NextFrame(() => TryApplyKnifeWithRetries(player, knifeDefinitionIndex.Value, 6));
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

        Server.NextFrame(() => GiveLoadoutItem(player, normalizedLoadout, index + 1, failedItems, knifeDefinitionIndex));
    }

    private void TryDropBomb(CCSPlayerController player, Action onCompleted)
    {
        try
        {
            player.ExecuteClientCommandFromServer("slot5");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DEBUG] Nie udało się przełączyć gracza {Player} na slot bomby.", player.PlayerName);
        }

        Server.NextFrame(() =>
        {
            if (player == null || !player.IsValid || player.Team != CsTeam.Terrorist)
            {
                onCompleted();
                return;
            }

            if (!PlayerHasBomb(player))
            {
                onCompleted();
                return;
            }

            try
            {
                player.DropActiveWeapon();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[DEBUG] Nie udało się zrzucić aktywnej broni gracza {Player}.", player.PlayerName);
            }

            if (PlayerHasBomb(player))
            {
                try
                {
                    player.ExecuteClientCommandFromServer("drop");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "[DEBUG] Nie udało się wykonać komendy drop dla gracza {Player}.", player.PlayerName);
                }
            }

            onCompleted();
        });
    }


    private bool PlayerHasBomb(CCSPlayerController player)
    {
        try
        {
            var pawn = player.PlayerPawn.Value;
            if (pawn == null || !player.PlayerPawn.IsValid)
            {
                return false;
            }

            var weaponServices = pawn.WeaponServices?.As<CCSPlayer_WeaponServices>();
            if (weaponServices == null)
            {
                return false;
            }

            foreach (var weaponHandle in weaponServices.MyWeapons)
            {
                var weapon = weaponHandle.Value;
                if (weapon == null || !weapon.IsValid)
                {
                    continue;
                }

                if (string.Equals(weapon.GetWeaponName(), "weapon_c4", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[DEBUG] Nie udało się sprawdzić, czy gracz {Player} ma C4.", player.PlayerName);
        }

        return false;
    }

    private string NormalizeWeaponName(string weaponName)
    {
        if (_logger == null) return string.Empty;
        _logger.LogWarning($"[DEBUG] NormalizeWeaponName {weaponName}");
        if (string.IsNullOrWhiteSpace(weaponName))
        {
            return string.Empty;
        }

        var trimmed = weaponName.Trim();
        var compactName = trimmed.Replace("-", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);

        if (trimmed.Contains("_ag2", StringComparison.OrdinalIgnoreCase) && trimmed.Contains("knife", StringComparison.OrdinalIgnoreCase))
        {
            return "weapon_knife";
        }

        if (WeaponAliases.TryGetValue(compactName, out var aliasWeapon))
        {
            return aliasWeapon;
        }

        if (trimmed.StartsWith("weapon_knife", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "weapon_bayonet", StringComparison.OrdinalIgnoreCase))
        {
            return "weapon_knife";
        }

        if (trimmed.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("item_", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.ToLowerInvariant();
        }

        if (Enum.TryParse<CsItem>(compactName, true, out var csItem))
        {
            var enumValue = EnumUtils.GetEnumMemberAttributeValue(csItem);
            if (!string.IsNullOrWhiteSpace(enumValue))
            {
                return enumValue;
            }
        }

        var snakeCase = trimmed.ToLowerInvariant()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);

        if (!snakeCase.StartsWith("weapon_", StringComparison.Ordinal) && !snakeCase.StartsWith("item_", StringComparison.Ordinal))
        {
            snakeCase = $"weapon_{snakeCase}";
        }

        return snakeCase;
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

