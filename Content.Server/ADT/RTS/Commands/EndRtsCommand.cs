using System.Linq;
using Content.Server.Administration;
using Content.Shared.ADT.RTS.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.ADT.RTS.Commands;

/// <summary>
/// endrts [игрок]
/// Прерывает матч указанного игрока, либо все матчи сразу.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class EndRtsCommand : LocalizedEntityCommands
{
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly RtsMatchSystem _match = default!;

    public override string Command => "endrts";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1)
        {
            shell.WriteError(Loc.GetString("rts-cmd-end-usage"));
            return;
        }

        if (args.Length == 1)
        {
            if (!_players.TryGetSessionByUsername(args[0], out var session))
            {
                shell.WriteError(Loc.GetString("rts-cmd-player-not-found", ("player", args[0])));
                return;
            }

            if (_match.GetMatch(session.UserId) is not { } match)
            {
                shell.WriteError(Loc.GetString("rts-cmd-end-not-in-match", ("player", session.Name)));
                return;
            }

            _match.AbortMatch(match);
            shell.WriteLine(Loc.GetString("rts-cmd-end-single", ("player", session.Name)));
            return;
        }

        var aborted = 0;
        var query = EntityManager.EntityQueryEnumerator<RtsMatchComponent>();

        while (query.MoveNext(out var uid, out _))
        {
            _match.AbortMatch(uid);
            aborted++;
        }

        shell.WriteLine(Loc.GetString("rts-cmd-end-all", ("count", aborted)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromHintOptions(_players.Sessions.Select(session => session.Name), Loc.GetString("rts-cmd-hint-player"));

        return CompletionResult.Empty;
    }
}
