using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server.ADT.RTS.Commands;

/// <summary>
/// initrts &lt;игрок1&gt; &lt;игрок2&gt; [туман]
/// Запускает RTS-матч между двумя призраками.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §2.1.
/// </summary>
[AdminCommand(AdminFlags.Fun)]
public sealed class InitRtsCommand : LocalizedEntityCommands
{
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly RtsMatchSystem _match = default!;

    public override string Command => "initrts";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            shell.WriteError(Loc.GetString("rts-cmd-init-usage"));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var first))
        {
            shell.WriteError(Loc.GetString("rts-cmd-player-not-found", ("player", args[0])));
            return;
        }

        if (!_players.TryGetSessionByUsername(args[1], out var second))
        {
            shell.WriteError(Loc.GetString("rts-cmd-player-not-found", ("player", args[1])));
            return;
        }

        var fogOfWar = true;

        if (args.Length == 3 && !bool.TryParse(args[2], out fogOfWar))
        {
            shell.WriteError(Loc.GetString("rts-cmd-bad-bool", ("value", args[2])));
            return;
        }

        if (!_match.TryStartMatch(first, second, fogOfWar, out var error))
        {
            shell.WriteError(error);
            return;
        }

        shell.WriteLine(Loc.GetString("rts-cmd-init-started",
            ("first", first.Name),
            ("second", second.Name),
            ("fog", fogOfWar)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        // Подсказываем только призраков: живого игрока команда всё равно не пустит.
        if (args.Length is 1 or 2)
        {
            var ghosts = _players.Sessions
                .Where(session => session.AttachedEntity != null && EntityManager.HasComponent<GhostComponent>(session.AttachedEntity))
                .Select(session => session.Name);

            return CompletionResult.FromHintOptions(ghosts, Loc.GetString("rts-cmd-hint-ghost"));
        }

        if (args.Length == 3)
            return CompletionResult.FromHintOptions(new[] { "true", "false" }, Loc.GetString("rts-cmd-hint-fog"));

        return CompletionResult.Empty;
    }
}
