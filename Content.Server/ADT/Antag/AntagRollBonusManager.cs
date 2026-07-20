using Content.Shared.ADT.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.ADT.Antag;

public sealed class AntagRollBonusManager
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ILogManager _logManager = default!;

    public const int MaxMissedRounds = 20;

    private ISawmill _sawmill = default!;

    private bool _enabled;
    private float _bonusPerRound;

    private readonly Dictionary<NetUserId, int> _missedRounds = new();

    private readonly HashSet<NetUserId> _preSelected = new();

    private readonly HashSet<NetUserId> _becameAntag = new();

    public void Initialize()
    {
        _sawmill = _logManager.GetSawmill("antag.rollbonus");

        _cfg.OnValueChanged(ADTCCVars.AntagRollBonusEnabled, OnEnabledChanged, true);
        _cfg.OnValueChanged(ADTCCVars.AntagRollBonusPerRound, OnBonusPerRoundChanged, true);

        _sawmill.Info($"Initialized. Enabled: {_enabled}, bonus per missed round: {_bonusPerRound:P0}, cap: {MaxMissedRounds} rounds.");
    }

    private void OnEnabledChanged(bool value)
    {
        _enabled = value;
        _sawmill.Info($"Roll bonus {(value ? "enabled" : "disabled")}.");
    }

    private void OnBonusPerRoundChanged(float value)
    {
        _bonusPerRound = value;
        _sawmill.Info($"Bonus per missed round set to {value:P0}. Max weight is now {1f + value * MaxMissedRounds:F2}.");
    }

    public float GetWeight(ICommonSession session)
    {
        if (!_enabled)
            return 1f;

        if (!_missedRounds.TryGetValue(session.UserId, out var missed))
            return 1f;

        return 1f + _bonusPerRound * missed;
    }

    public int GetMissedRounds(ICommonSession session)
    {
        return _missedRounds.GetValueOrDefault(session.UserId);
    }

    public void MarkPreSelected(ICommonSession session)
    {
        if (!_enabled)
            return;

        _preSelected.Add(session.UserId);
    }

    public void MarkBecameAntag(ICommonSession session)
    {
        if (!_enabled)
            return;

        if (!_preSelected.Contains(session.UserId))
            return;

        _becameAntag.Add(session.UserId);
    }

    public void FinishRound(IEnumerable<NetUserId> participants)
    {
        if (!_enabled)
        {
            ClearRoundState();
            return;
        }

        var reset = 0;
        var increased = 0;
        var capped = 0;

        foreach (var user in participants)
        {
            if (_becameAntag.Contains(user))
            {
                _missedRounds.Remove(user);
                reset++;
                continue;
            }

            var missed = _missedRounds.GetValueOrDefault(user);
            if (missed >= MaxMissedRounds)
            {
                capped++;
                continue;
            }

            _missedRounds[user] = missed + 1;
            increased++;
        }

        _preSelected.Clear();
        _becameAntag.Clear();
    }

    public void ClearRoundState()
    {
        _preSelected.Clear();
        _becameAntag.Clear();
    }
}
