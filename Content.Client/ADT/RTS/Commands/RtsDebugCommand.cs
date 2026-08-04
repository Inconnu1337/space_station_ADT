using System.Linq;
using Content.Shared.ADT.RTS.Components;
using Robust.Client.Player;
using Robust.Shared.Console;

namespace Content.Client.ADT.RTS.Commands;

/// <summary>
/// rtsdebug — печатает состояние RTS на клиенте.
///
/// Нужна, чтобы отличать друг от друга три причины «ничего не выделяется»:
/// сущности не дошли до клиента (PVS), не доехало состояние команды, или
/// не сходится владение.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §2.1.
/// </summary>
public sealed class RtsDebugCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override string Command => "rtsdebug";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_players.LocalEntity is not { } local)
        {
            shell.WriteError("Нет локальной сущности.");
            return;
        }

        shell.WriteLine($"Локальная сущность: {local} ({EntityManager.ToPrettyString(local)})");

        if (!EntityManager.TryGetComponent<RtsCameraComponent>(local, out var camera))
        {
            shell.WriteError("На локальной сущности НЕТ RtsCameraComponent — игрок не в матче " +
                             "или состояние камеры не дошло до клиента.");
            return;
        }

        shell.WriteLine($"Камера: TeamIndex={camera.TeamIndex}, Team={camera.Team}, Match={camera.Match}");

        if (camera.TeamIndex < 0 && camera.Team == null)
            shell.WriteError("Ни индекс команды, ни её сущность не доехали — выделять нечем.");

        var position = _xform.GetWorldPosition(local);

        var selectable = EntityManager.EntityQueryEnumerator<RtsSelectableComponent, TransformComponent>();
        var total = 0;
        var mine = 0;
        var lines = new List<string>();

        while (selectable.MoveNext(out var uid, out var select, out var xform))
        {
            total++;

            EntityManager.TryGetComponent<RtsOwnedComponent>(uid, out var owned);
            var ownIndex = owned?.TeamIndex ?? -1;
            var ownTeam = owned?.Team;

            if (ownIndex == camera.TeamIndex || (camera.Team != null && ownTeam == camera.Team))
                mine++;

            if (lines.Count >= 12)
                continue;

            var distance = (_xform.GetWorldPosition(xform) - position).Length();
            lines.Add($"  {EntityManager.ToPrettyString(uid)} owned={owned != null} " +
                      $"teamIndex={ownIndex} team={ownTeam} priority={select.Priority} dist={distance:F1}");
        }

        shell.WriteLine($"Выделяемых сущностей на клиенте: {total}, из них своих: {mine}");

        if (total == 0)
            shell.WriteError("Клиент не видит НИ ОДНОЙ выделяемой сущности — проблема в PVS " +
                             "или в масках видимости, а не в выделении.");

        foreach (var line in lines)
        {
            shell.WriteLine(line);
        }
    }
}
