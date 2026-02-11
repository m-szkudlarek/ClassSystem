using CounterStrikeSharp.API.Modules.Entities;

namespace ClassSystem.Runtime;

public sealed class PlayerState
{
    public PlayerState(int slot)
    {
        Slot = slot;
    }

    public int Slot { get; }
    public int? UserId { get; private set; }
    public ulong? SteamId64 { get; private set; }
    public SteamID? AuthorizedSteamId { get; private set; }
    public bool IsRegisteredInSession { get; set; }

    public string? SelectedClassId { get; set; }
    public bool SelectedClassThisRound { get; set; }
    public RuntimeClass? RuntimeClass { get; set; }

    private readonly List<Action<SteamID>> _pendingSteamActions = [];

    public void SetUserId(int? userId)
    {
        UserId = userId;
    }

    public void SetSteamId(SteamID steamId)
    {
        AuthorizedSteamId = steamId;
        SteamId64 = steamId.SteamId64;
    }

    public void EnqueueSteamAction(Action<SteamID> action)
    {
        if (AuthorizedSteamId != null)
        {
            action(AuthorizedSteamId);
            return;
        }

        _pendingSteamActions.Add(action);
    }

    public void DrainPendingSteamActions()
    {
        if (AuthorizedSteamId == null || _pendingSteamActions.Count == 0)
        {
            return;
        }

        foreach (var action in _pendingSteamActions)
        {
            action(AuthorizedSteamId);
        }

        _pendingSteamActions.Clear();
    }

    public void ResetRound()
    {
        SelectedClassThisRound = false;
        RuntimeClass?.ResetRound();
    }
}
