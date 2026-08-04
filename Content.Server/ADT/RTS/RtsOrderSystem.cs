using System.Numerics;
using Content.Shared.ADT.RTS;
using Content.Shared.ADT.RTS.Components;
using Robust.Shared.Map;

namespace Content.Server.ADT.RTS;

/// <summary>
/// Приём приказов от клиента. Выделение живёт на клиенте, поэтому каждая присланная
/// сущность перепроверяется: принадлежит ли она команде отправителя.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §4.2.
/// </summary>
public sealed class RtsOrderSystem : EntitySystem
{
    [Dependency] private readonly RtsMatchSystem _match = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>
    /// Сколько сущностей максимум принимаем в одном приказе. Защита от пакета,
    /// в котором клиент прислал полмиллиона идентификаторов.
    /// </summary>
    private const int MaxUnitsPerOrder = 400;

    /// <summary>
    /// На сколько тайлов расходится точка прибытия для группы. Без этого вся группа
    /// целится в один тайл и толкается на месте.
    /// </summary>
    private const float GroupSpreadPerUnit = 0.28f;

    private readonly List<Entity<RtsMovementComponent, RtsOrderQueueComponent>> _targets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RtsOrderEvent>(OnOrder);
    }

    private void OnOrder(RtsOrderEvent args, EntitySessionEventArgs session)
    {
        if (_match.GetMatch(session.SenderSession.UserId) is not { } matchUid)
            return;

        if (!TryComp<RtsMatchComponent>(matchUid, out var match) || match.Stage != RtsMatchStage.Running)
            return;

        if (_match.GetTeam(matchUid, session.SenderSession.UserId) is not { } teamUid)
            return;

        if (args.Units.Length is 0 or > MaxUnitsPerOrder)
            return;

        _targets.Clear();

        foreach (var netEntity in args.Units)
        {
            if (!TryGetEntity(netEntity, out var uid))
                continue;

            // Единственная защита от «поуправляй чужой армией»: владение проверяем здесь.
            if (!TryComp<RtsOwnedComponent>(uid, out var owned) || owned.Team != teamUid)
                continue;

            if (!TryComp<RtsMovementComponent>(uid, out var movement) || !TryComp<RtsOrderQueueComponent>(uid, out var orders))
                continue;

            _targets.Add((uid.Value, movement, orders));
        }

        if (_targets.Count == 0)
            return;

        switch (args.Type)
        {
            case RtsOrderType.Stop:
                ExecuteStop();
                break;
            case RtsOrderType.HoldPosition:
                ExecuteHold();
                break;
            default:
                ExecuteMove(args);
                break;
        }
    }

    private void ExecuteStop()
    {
        foreach (var target in _targets)
        {
            target.Comp1.Target = null;
            target.Comp2.Queue.Clear();
            target.Comp2.State = RtsUnitState.Idle;
            Dirty(target.Owner, target.Comp1);
            Dirty(target.Owner, target.Comp2);
        }
    }

    private void ExecuteHold()
    {
        foreach (var target in _targets)
        {
            target.Comp1.Target = null;
            target.Comp2.Queue.Clear();
            target.Comp2.State = RtsUnitState.HoldPosition;
            Dirty(target.Owner, target.Comp1);
            Dirty(target.Owner, target.Comp2);
        }
    }

    private void ExecuteMove(RtsOrderEvent args)
    {
        if (args.Target is not { } netCoordinates)
            return;

        var coordinates = GetCoordinates(netCoordinates);

        if (!coordinates.IsValid(EntityManager))
            return;

        var destination = _xform.ToMapCoordinates(coordinates).Position;

        // Радиус прибытия растёт от размера группы: двадцать юнитов физически не встанут
        // в один тайл, и без запаса они будут вечно толкаться у точки приказа.
        var arrival = 0.35f + MathF.Sqrt(_targets.Count) * GroupSpreadPerUnit;

        foreach (var target in _targets)
        {
            var movement = target.Comp1;
            var orders = target.Comp2;

            movement.ArrivalDistance = arrival;
            movement.StuckTime = 0f;

            if (args.Queue && orders.Queue.Count < RtsOrderQueueComponent.MaxQueue)
            {
                orders.Queue.Add(new RtsQueuedOrder
                {
                    Type = args.Type,
                    Position = destination,
                });

                // Если юнит стоит, сразу берём первый приказ из очереди в работу.
                if (movement.Target == null)
                {
                    movement.Target = orders.Queue[0].Position;
                    orders.Queue.RemoveAt(0);
                }
            }
            else
            {
                orders.Queue.Clear();
                movement.Target = destination;
            }

            orders.State = args.Type == RtsOrderType.AttackMove ? RtsUnitState.AttackMove : RtsUnitState.Moving;
            Dirty(target.Owner, movement);
            Dirty(target.Owner, orders);
        }
    }
}
