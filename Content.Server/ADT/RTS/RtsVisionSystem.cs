using System.Linq;
using System.Numerics;
using Content.Shared.ADT.RTS;
using Content.Shared.ADT.RTS.Components;
using Content.Shared.Eye;
using Robust.Shared.Timing;

namespace Content.Server.ADT.RTS;

/// <summary>
/// Туман войны: считает видимые тайлы каждой команды и прячет вражеские сущности из PVS
/// через слои видимости.
///
/// Прятать нужно именно на уровне PVS, а не рисованием черноты поверх: иначе состояние
/// врага доходит до клиента и читается любым читом.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §6.
/// </summary>
public sealed class RtsVisionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedVisibilitySystem _visibility = default!;

    /// <summary>
    /// Частота пересчёта. Чаще незачем: за 250 мс юнит проходит меньше тайла.
    /// </summary>
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Сколько времени сущность остаётся видимой после того, как её перестали освещать.
    /// Без этого юнит на границе обзора мерцает, а каждое мерцание — пересчёт PVS.
    /// </summary>
    private static readonly TimeSpan VisibilityHysteresis = TimeSpan.FromSeconds(2);

    private static readonly int[] TeamFlags =
    {
        (int) VisibilityFlags.RtsTeamA,
        (int) VisibilityFlags.RtsTeamB,
    };

    private TimeSpan _nextUpdate;

    private readonly Dictionary<EntityUid, RtsFogData> _fog = new();

    /// <summary>
    /// Когда сущность в последний раз была видна команде. Ключ — сущность и индекс команды.
    /// </summary>
    private readonly Dictionary<(EntityUid, int), TimeSpan> _lastSeen = new();

    /// <summary>
    /// Бит видимости для команды с указанным индексом.
    /// </summary>
    public static int GetTeamFlag(int index)
    {
        if (index < 0 || index >= TeamFlags.Length)
            return 0;

        return TeamFlags[index];
    }

    /// <summary>
    /// Туман команды. Появляется вместе с первым пересчётом.
    /// </summary>
    public RtsFogData? GetFog(EntityUid team)
    {
        return _fog.GetValueOrDefault(team);
    }

    public void ForgetTeam(EntityUid team)
    {
        _fog.Remove(team);
    }

    /// <summary>
    /// Настраивает камеру игрока: она видит только свою команду и то, что этой команде
    /// подсвечено. При выключенном тумане камера видит обе команды сразу.
    /// </summary>
    public void SetupCamera(EntityUid camera, int teamIndex, bool fogOfWar)
    {
        var mask = (int) VisibilityFlags.Normal;

        if (fogOfWar)
        {
            mask |= GetTeamFlag(teamIndex);
        }
        else
        {
            foreach (var flag in TeamFlags)
            {
                mask |= flag;
            }
        }

        _eye.SetVisibilityMask(camera, mask);
    }

    /// <summary>
    /// Вешает на сущность бит её команды. Без него собственные юниты не видны владельцу.
    /// </summary>
    public void SetupEntity(EntityUid uid, int teamIndex, bool fogOfWar)
    {
        var visibility = EnsureComp<VisibilityComponent>(uid);

        // Normal снимаем намеренно: иначе сущность видна всем, у кого он в маске,
        // и весь туман теряет смысл.
        var layer = GetTeamFlag(teamIndex);

        if (!fogOfWar)
        {
            foreach (var flag in TeamFlags)
            {
                layer |= flag;
            }
        }

        _visibility.SetLayer((uid, visibility), (ushort) layer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<RtsMatchComponent>();
        while (query.MoveNext(out var uid, out var match))
        {
            if (match.Stage is not (RtsMatchStage.Running or RtsMatchStage.Countdown))
                continue;

            if (!match.FogOfWar)
                continue;

            UpdateMatch((uid, match));
        }
    }

    private void UpdateMatch(Entity<RtsMatchComponent> match)
    {
        foreach (var teamUid in match.Comp.Teams)
        {
            if (!TryComp<RtsTeamComponent>(teamUid, out var team))
                continue;

            if (!_fog.TryGetValue(teamUid, out var fog))
            {
                fog = new RtsFogData(match.Comp.ArenaSize);
                _fog[teamUid] = fog;
            }

            fog.ClearVisible();
            CastVision(teamUid, fog);
        }

        UpdateEntityVisibility(match);
    }

    /// <summary>
    /// Засвечивает круги вокруг всех сущностей команды с обзором.
    /// </summary>
    private void CastVision(EntityUid teamUid, RtsFogData fog)
    {
        var query = EntityQueryEnumerator<RtsVisionComponent, RtsOwnedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var vision, out var owned, out var xform))
        {
            if (owned.Team != teamUid)
                continue;

            var center = RtsPathfindingSystem.ToTile(_xform.GetWorldPosition(xform));
            var radius = (int) MathF.Ceiling(vision.Range);
            var squared = vision.Range * vision.Range;

            for (var dx = -radius; dx <= radius; dx++)
            {
                for (var dy = -radius; dy <= radius; dy++)
                {
                    if (dx * dx + dy * dy > squared)
                        continue;

                    fog.MarkVisible(center + new Vector2i(dx, dy));
                }
            }
        }
    }

    /// <summary>
    /// Раздаёт и снимает биты видимости на сущностях матча.
    /// </summary>
    private void UpdateEntityVisibility(Entity<RtsMatchComponent> match)
    {
        var query = EntityQueryEnumerator<RtsOwnedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var owned, out var xform))
        {
            if (owned.Team is not { } ownerTeam || !match.Comp.Teams.Contains(ownerTeam))
                continue;

            if (!TryComp<VisibilityComponent>(uid, out var visibility))
                continue;

            var tile = RtsPathfindingSystem.ToTile(_xform.GetWorldPosition(xform));

            for (var index = 0; index < match.Comp.Teams.Count; index++)
            {
                var viewerTeam = match.Comp.Teams[index];

                // Свои сущности всегда видны своим — их бит не трогаем.
                if (viewerTeam == ownerTeam)
                    continue;

                if (!TryComp<RtsTeamComponent>(viewerTeam, out var viewer))
                    continue;

                var flag = GetTeamFlag(viewer.Index);

                if (flag == 0)
                    continue;

                var seen = _fog.TryGetValue(viewerTeam, out var fog) && fog.IsVisible(tile);
                ApplyVisibility(uid, visibility, viewer.Index, (ushort) flag, seen);
            }
        }
    }

    /// <summary>
    /// Гистерезис: бит снимается не сразу, а через паузу после потери из виду.
    /// </summary>
    private void ApplyVisibility(EntityUid uid, VisibilityComponent visibility, int viewerIndex, ushort flag, bool seen)
    {
        var key = (uid, viewerIndex);

        if (seen)
        {
            _lastSeen[key] = _timing.CurTime;
            _visibility.AddLayer((uid, visibility), flag);
            return;
        }

        if (!_lastSeen.TryGetValue(key, out var last))
            return;

        if (_timing.CurTime - last < VisibilityHysteresis)
            return;

        _lastSeen.Remove(key);
        _visibility.RemoveLayer((uid, visibility), flag);
    }
}
