using System.Numerics;
using Content.Shared.ADT.RTS;
using Content.Shared.ADT.RTS.Components;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.ADT.RTS;

/// <summary>
/// Жизненный цикл RTS-матча: запуск, стадии, вход и выход игроков, уборка.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §2.
/// </summary>
public sealed class RtsMatchSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly RtsMapGenSystem _mapGen = default!;
    [Dependency] private readonly RtsPathfindingSystem _pathfinding = default!;
    [Dependency] private readonly RtsVisionSystem _vision = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    /// <summary>
    /// Прототип сущности-камеры. Это не призрак: см. §4.1 плана.
    /// </summary>
    private const string CameraProto = "ADTRtsCamera";

    private const string TownHallProto = "ADTRtsTownHall";
    private const string WorkerProto = "ADTRtsWorker";

    private const int StartingWorkers = 5;
    private const float WorkerSpawnRadius = 2.5f;

    private const int MaxGenerationAttempts = 5;

    private static readonly TimeSpan CountdownTime = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FinishedTime = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DisconnectGrace = TimeSpan.FromSeconds(60);

    private static readonly Color[] TeamColors =
    {
        Color.FromHex("#3AA33A"),
        Color.FromHex("#B03030"),
        Color.FromHex("#3060B0"),
        Color.FromHex("#B0A030"),
    };

    /// <summary>
    /// Кто сейчас в матче. Не даём одному игроку сидеть в двух матчах сразу.
    /// </summary>
    private readonly Dictionary<NetUserId, EntityUid> _activeUsers = new();

    private readonly List<EntityUid> _cleanupBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<RtsLeaveMatchEvent>(OnLeaveRequest);
    }

    /// <summary>
    /// Игрок нажал «покинуть матч». Для соперника это победа.
    /// </summary>
    private void OnLeaveRequest(RtsLeaveMatchEvent args, EntitySessionEventArgs session)
    {
        if (GetMatch(session.SenderSession.UserId) is not { } matchUid)
            return;

        if (!TryComp<RtsMatchComponent>(matchUid, out var match))
            return;

        foreach (var teamUid in match.Teams)
        {
            if (!TryComp<RtsTeamComponent>(teamUid, out var team) || team.User != session.SenderSession.UserId)
                continue;

            DefeatTeam((matchUid, match), (teamUid, team), RtsMatchEndReason.Surrender);
            break;
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _cleanupBuffer.Clear();

        var query = EntityQueryEnumerator<RtsMatchComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            _cleanupBuffer.Add(uid);
        }

        foreach (var uid in _cleanupBuffer)
        {
            CleanupMatch(uid);
        }
    }

    /// <summary>
    /// Запускает матч между двумя призраками.
    /// </summary>
    /// <param name="second">
    /// Соперник. null — одиночный матч: команда одна, соперника нет. Нужен для тестов,
    /// чтобы не звать второго человека ради проверки экономики или пасфайндинга.
    /// </param>
    public bool TryStartMatch(ICommonSession first, ICommonSession? second, bool fogOfWar, out string error)
    {
        if (second != null && first == second)
        {
            error = Loc.GetString("rts-cmd-same-player");
            return false;
        }

        if (!TryValidatePlayer(first, out var firstMind, out error))
            return false;

        var secondMind = EntityUid.Invalid;

        if (second != null && !TryValidatePlayer(second, out secondMind, out error))
            return false;

        var match = Spawn(null, MapCoordinates.Nullspace);
        var matchComp = AddComp<RtsMatchComponent>(match);

        matchComp.Stage = RtsMatchStage.Generating;
        matchComp.FogOfWar = fogOfWar;
        matchComp.Seed = _random.Next();
        matchComp.Attempt = 0;

        CreateTeam((match, matchComp), first, firstMind, 0);

        if (second != null)
            CreateTeam((match, matchComp), second, secondMind, 1);

        _mapGen.CreateArenaMap((match, matchComp));

        error = string.Empty;
        return true;
    }

    private bool TryValidatePlayer(ICommonSession session, out EntityUid mindId, out string error)
    {
        mindId = default;

        if (_activeUsers.ContainsKey(session.UserId))
        {
            error = Loc.GetString("rts-cmd-already-in-match", ("player", session.Name));
            return false;
        }

        if (session.AttachedEntity is not { } attached || !HasComp<GhostComponent>(attached))
        {
            error = Loc.GetString("rts-cmd-not-ghost", ("player", session.Name));
            return false;
        }

        if (!_mind.TryGetMind(session, out var foundMind, out var mind))
        {
            error = Loc.GetString("rts-cmd-no-mind", ("player", session.Name));
            return false;
        }

        // Visit нельзя вложить в Visit: админ-призрак уже занимает этот механизм.
        if (mind.VisitingEntity != null)
        {
            error = Loc.GetString("rts-cmd-already-visiting", ("player", session.Name));
            return false;
        }

        mindId = foundMind;
        error = string.Empty;
        return true;
    }

    private void CreateTeam(Entity<RtsMatchComponent> match, ICommonSession session, EntityUid mindId, int index)
    {
        var team = Spawn(null, MapCoordinates.Nullspace);
        var teamComp = AddComp<RtsTeamComponent>(team);

        teamComp.Match = match.Owner;
        teamComp.Index = index;
        teamComp.Color = TeamColors[index % TeamColors.Length];
        teamComp.User = session.UserId;
        teamComp.MindId = mindId;
        teamComp.Ghost = session.AttachedEntity;

        // Состояние команды нужно владельцу всегда, независимо от того, куда смотрит камера.
        _pvs.AddSessionOverride(team, session);

        match.Comp.Teams.Add(team);
        _activeUsers[session.UserId] = match.Owner;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _cleanupBuffer.Clear();

        var query = EntityQueryEnumerator<RtsMatchComponent>();
        while (query.MoveNext(out var uid, out var match))
        {
            switch (match.Stage)
            {
                case RtsMatchStage.Generating:
                    UpdateGenerating((uid, match));
                    break;
                case RtsMatchStage.Countdown:
                    UpdateCountdown((uid, match));
                    break;
                case RtsMatchStage.Running:
                    UpdateRunning((uid, match));
                    break;
                case RtsMatchStage.Finished:
                    if (_timing.CurTime >= match.StageEnd)
                        match.Stage = RtsMatchStage.Cleanup;
                    break;
                case RtsMatchStage.Cleanup:
                    _cleanupBuffer.Add(uid);
                    break;
            }
        }

        foreach (var uid in _cleanupBuffer)
        {
            CleanupMatch(uid);
        }
    }

    private void UpdateGenerating(Entity<RtsMatchComponent> match)
    {
        if (!_mapGen.TickGeneration(match))
            return;

        if (_mapGen.TryFinishArena(match))
        {
            AttachPlayers(match);
            match.Comp.Stage = RtsMatchStage.Countdown;
            match.Comp.StageEnd = _timing.CurTime + CountdownTime;
            return;
        }

        // Арена не сошлась: несвязные базы или не нашлось места под старт.
        match.Comp.Attempt++;

        if (match.Comp.Attempt >= MaxGenerationAttempts)
        {
            Log.Error($"RTS: не удалось сгенерировать арену за {MaxGenerationAttempts} попыток, матч отменён.");
            match.Comp.EndReason = RtsMatchEndReason.Aborted;
            match.Comp.Stage = RtsMatchStage.Cleanup;
            return;
        }

        DeleteArenaMap(match.Comp);
        match.Comp.Seed = _random.Next();
        _mapGen.CreateArenaMap(match);
    }

    private void UpdateCountdown(Entity<RtsMatchComponent> match)
    {
        if (_timing.CurTime < match.Comp.StageEnd)
            return;

        match.Comp.Stage = RtsMatchStage.Running;
    }

    private void UpdateRunning(Entity<RtsMatchComponent> match)
    {
        foreach (var teamUid in match.Comp.Teams)
        {
            if (!TryComp<RtsTeamComponent>(teamUid, out var team) || team.Defeated)
                continue;

            if (team.User is not { } user)
                continue;

            if (!_players.TryGetSessionById(user, out var session))
            {
                // Игрок отключился: даём время вернуться.
                team.DisconnectDeadline ??= _timing.CurTime + DisconnectGrace;

                if (_timing.CurTime >= team.DisconnectDeadline)
                    DefeatTeam(match, (teamUid, team), RtsMatchEndReason.Disconnected);

                continue;
            }

            team.DisconnectDeadline = null;

            // Игрока вернули в тело, зареспавнили или его камеру удалили — это сдача.
            if (team.Camera is not { } camera || !Exists(camera) || session.AttachedEntity != camera)
                DefeatTeam(match, (teamUid, team), RtsMatchEndReason.Surrender);
        }
    }

    /// <summary>
    /// Стартовая база: ратуша и несколько рабочих вокруг неё.
    /// Здания получат смысл в фазе 6, пока ратуша — просто выделяемая цель.
    /// </summary>
    private void SpawnStartingBase(Entity<RtsMatchComponent> match, Entity<RtsTeamComponent> team, Vector2 center)
    {
        var mapId = match.Comp.MapId;
        var hall = Spawn(TownHallProto, new MapCoordinates(center, mapId));
        SetOwner(hall, team);

        for (var i = 0; i < StartingWorkers; i++)
        {
            // Раскладываем рабочих по кругу, чтобы они не появились друг в друге.
            var angle = MathF.Tau * i / StartingWorkers;
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * WorkerSpawnRadius;
            var worker = Spawn(WorkerProto, new MapCoordinates(center + offset, mapId));
            SetOwner(worker, team);
        }
    }

    /// <summary>
    /// Привязывает сущность к команде и красит её в цвет команды.
    /// </summary>
    private void SetOwner(EntityUid uid, Entity<RtsTeamComponent> team)
    {
        var owned = EnsureComp<RtsOwnedComponent>(uid);
        owned.Team = team.Owner;
        owned.TeamIndex = team.Comp.Index;
        Dirty(uid, owned);

        if (TryComp<RtsMatchComponent>(team.Comp.Match, out var match))
            _vision.SetupEntity(uid, team.Comp.Index, match.FogOfWar);

        // Свои сущности всегда держим у владельца в PVS. Иначе они выгружаются, стоит
        // камере отъехать, и вместе с ними пропадают линии приказов, отметки на миникарте
        // и засветка тумана — клиент считает туман именно по своим юнитам.
        if (team.Comp.User is { } user && _players.TryGetSessionById(user, out var session))
            _pvs.AddSessionOverride(uid, session);
    }

    private void AttachPlayers(Entity<RtsMatchComponent> match)
    {
        if (match.Comp.MapId == MapId.Nullspace)
            return;

        var bounds = _mapGen.GetArenaBounds(match.Comp);

        foreach (var teamUid in match.Comp.Teams)
        {
            if (!TryComp<RtsTeamComponent>(teamUid, out var team))
                continue;

            var position = new Vector2(team.SpawnTile.X + 0.5f, team.SpawnTile.Y + 0.5f);

            SpawnStartingBase(match, (teamUid, team), position);
            var camera = Spawn(CameraProto, new MapCoordinates(position, match.Comp.MapId));

            var cameraComp = EnsureComp<RtsCameraComponent>(camera);
            cameraComp.Team = teamUid;
            cameraComp.TeamIndex = team.Index;
            cameraComp.Match = match.Owner;
            cameraComp.Bounds = bounds;
            cameraComp.FogOfWar = match.Comp.FogOfWar;
            Dirty(camera, cameraComp);

            _vision.SetupCamera(camera, team.Index, match.Comp.FogOfWar);

            team.Camera = camera;

            if (team.MindId is { } mindId)
                _mind.Visit(mindId, camera);
        }
    }

    /// <summary>
    /// Поражение одной команды. Пока команд ровно две, это сразу же конец матча.
    /// </summary>
    public void DefeatTeam(Entity<RtsMatchComponent> match, Entity<RtsTeamComponent> team, RtsMatchEndReason reason)
    {
        if (team.Comp.Defeated)
            return;

        team.Comp.Defeated = true;
        Dirty(team.Owner, team.Comp);

        EntityUid? winner = null;

        foreach (var other in match.Comp.Teams)
        {
            if (other == team.Owner || !TryComp<RtsTeamComponent>(other, out var otherTeam) || otherTeam.Defeated)
                continue;

            winner = other;
            break;
        }

        FinishMatch(match, winner, reason);
    }

    /// <summary>
    /// Переводит матч в стадию показа результата.
    /// </summary>
    public void FinishMatch(Entity<RtsMatchComponent> match, EntityUid? winner, RtsMatchEndReason reason)
    {
        if (match.Comp.Stage is RtsMatchStage.Finished or RtsMatchStage.Cleanup)
            return;

        match.Comp.Winner = winner;
        match.Comp.EndReason = reason;
        match.Comp.Stage = RtsMatchStage.Finished;
        match.Comp.StageEnd = _timing.CurTime + FinishedTime;
        Dirty(match.Owner, match.Comp);
    }

    /// <summary>
    /// Немедленно прерывает матч и запускает уборку, минуя экран результата.
    /// </summary>
    public void AbortMatch(EntityUid match)
    {
        if (!TryComp<RtsMatchComponent>(match, out var comp))
            return;

        comp.EndReason = RtsMatchEndReason.Aborted;
        comp.Stage = RtsMatchStage.Cleanup;
    }

    /// <summary>
    /// Матч, в котором сейчас находится игрок.
    /// </summary>
    public EntityUid? GetMatch(NetUserId user)
    {
        if (_activeUsers.TryGetValue(user, out var match))
            return match;

        return null;
    }

    /// <summary>
    /// Команда игрока в конкретном матче.
    /// </summary>
    public EntityUid? GetTeam(EntityUid match, NetUserId user)
    {
        if (!TryComp<RtsMatchComponent>(match, out var comp))
            return null;

        foreach (var teamUid in comp.Teams)
        {
            if (TryComp<RtsTeamComponent>(teamUid, out var team) && team.User == user)
                return teamUid;
        }

        return null;
    }

    public bool HasMatches()
    {
        var query = EntityQueryEnumerator<RtsMatchComponent>();
        return query.MoveNext(out _, out _);
    }

    private void CleanupMatch(EntityUid uid)
    {
        if (!TryComp<RtsMatchComponent>(uid, out var match))
            return;

        foreach (var teamUid in match.Teams)
        {
            if (!TryComp<RtsTeamComponent>(teamUid, out var team))
                continue;

            ReturnPlayer((teamUid, team));
            _vision.ForgetTeam(teamUid);
            QueueDel(teamUid);
        }

        match.Teams.Clear();

        DeleteArenaMap(match);
        _mapGen.ForgetArena(uid);
        QueueDel(uid);
    }

    /// <summary>
    /// Возвращает игрока в его призрака и убирает камеру.
    /// </summary>
    private void ReturnPlayer(Entity<RtsTeamComponent> team)
    {
        var comp = team.Comp;

        if (comp.User is { } user)
        {
            _activeUsers.Remove(user);

            if (_players.TryGetSessionById(user, out var session))
                _pvs.RemoveSessionOverride(team.Owner, session);
        }

        // UnVisit возвращает управление той сущности, которой мозг владеет — призраку.
        if (comp.MindId is { } mindId && Exists(mindId))
            _mind.UnVisit(mindId);

        if (comp.Camera is { } camera && Exists(camera))
            QueueDel(camera);

        comp.Camera = null;
    }

    private void DeleteArenaMap(RtsMatchComponent match)
    {
        // Кэш flow field'ов привязан к карте, а не к матчу: чистим до удаления карты.
        if (match.MapUid is { } mapUid)
            _pathfinding.ForgetArena(mapUid);

        if (match.MapId != MapId.Nullspace && _map.MapExists(match.MapId))
            _map.DeleteMap(match.MapId);

        match.MapUid = null;
        match.MapId = MapId.Nullspace;
    }
}
