using System.Numerics;
using Content.Shared.ADT.RTS;
using Content.Shared.ADT.RTS.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Server.ADT.RTS;

/// <summary>
/// Движение RTS-юнитов. Своё, а не SharedMoverController: у юнита нет игрока за спиной,
/// нужен только вектор «куда идти» из flow field плюс расталкивание соседей.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §7.2.
/// </summary>
public sealed class RtsMovementSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly RtsPathfindingSystem _pathfinding = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>
    /// Как часто пересчитывается расталкивание. Каждый тик это делать незачем: соседи
    /// за 100 мс далеко не уедут, а запросов к лукапу становится втрое меньше.
    /// </summary>
    private const float SeparationInterval = 0.1f;

    /// <summary>
    /// Радиус, в котором юниты расталкивают друг друга.
    /// </summary>
    private const float SeparationRange = 1.1f;

    private const float SeparationStrength = 1.6f;

    /// <summary>
    /// Ближе этого расстояния до цели переключаемся с flow field на движение напрямую:
    /// поле даёт направление по центрам тайлов и у самой цели выглядит ступенчато.
    /// </summary>
    private const float DirectDistance = 2.5f;

    /// <summary>
    /// Сколько секунд юнит может почти не двигаться, прежде чем мы признаем приказ
    /// невыполнимым. Без этого зажатый юнит будет вечно давить в стену.
    /// </summary>
    private const float StuckTimeout = 1.5f;

    private float _separationAccumulator;

    private readonly HashSet<Entity<RtsUnitComponent>> _neighbours = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _separationAccumulator += frameTime;
        var updateSeparation = _separationAccumulator >= SeparationInterval;

        if (updateSeparation)
            _separationAccumulator = 0f;

        var query = EntityQueryEnumerator<RtsMovementComponent, RtsOrderQueueComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var movement, out var orders, out var body, out var xform))
        {
            var position = _xform.GetWorldPosition(xform);

            if (movement.Target is not { } target)
            {
                Decelerate(uid, body, movement, frameTime);
                continue;
            }

            var toTarget = target - position;

            if (toTarget.Length() <= movement.ArrivalDistance)
            {
                FinishMovement(uid, body, movement, orders);
                continue;
            }

            if (updateSeparation)
                movement.Separation = GetSeparation(uid, xform, position);

            var direction = GetDirection(xform, position, target, toTarget);

            if (direction == Vector2.Zero)
            {
                FinishMovement(uid, body, movement, orders);
                continue;
            }

            var desired = direction * movement.Speed + movement.Separation * SeparationStrength;

            if (desired.Length() > movement.Speed)
                desired = Vector2.Normalize(desired) * movement.Speed;

            var velocity = Vector2.Lerp(body.LinearVelocity, desired, Math.Min(movement.Acceleration * frameTime / movement.Speed, 1f));
            _physics.SetLinearVelocity(uid, velocity, body: body);

            UpdateStuck(uid, body, movement, orders, position, frameTime);
        }
    }

    /// <summary>
    /// Пока цель далеко — идём по flow field, у самой цели — напрямую.
    /// </summary>
    private Vector2 GetDirection(TransformComponent xform, Vector2 position, Vector2 target, Vector2 toTarget)
    {
        if (toTarget.Length() <= DirectDistance)
            return Vector2.Normalize(toTarget);

        if (xform.MapUid is { } map)
        {
            var from = RtsPathfindingSystem.ToTile(position);
            var destination = RtsPathfindingSystem.ToTile(target);

            if (_pathfinding.TryGetDirection(map, from, destination, out var flow))
                return flow;
        }

        // Поля нет или цель недостижима — пробуем хотя бы напрямую.
        return Vector2.Normalize(toTarget);
    }

    /// <summary>
    /// Простое расталкивание соседей. Не полноценный RVO: для RTS хватает того, чтобы
    /// юниты не слипались в одну точку.
    /// </summary>
    private Vector2 GetSeparation(EntityUid uid, TransformComponent xform, Vector2 position)
    {
        _neighbours.Clear();
        _lookup.GetEntitiesInRange(_xform.GetMapCoordinates(uid, xform), SeparationRange, _neighbours);

        var push = Vector2.Zero;

        foreach (var neighbour in _neighbours)
        {
            if (neighbour.Owner == uid)
                continue;

            var offset = position - _xform.GetWorldPosition(neighbour.Owner);
            var distance = offset.Length();

            if (distance <= 0.001f)
            {
                // Ровно одна точка: расходимся хоть куда-нибудь, иначе делить на ноль.
                push += new Vector2(0.01f, 0f);
                continue;
            }

            push += offset / distance * (1f - Math.Min(distance / SeparationRange, 1f));
        }

        return push;
    }

    private void UpdateStuck(
        EntityUid uid,
        PhysicsComponent body,
        RtsMovementComponent movement,
        RtsOrderQueueComponent orders,
        Vector2 position,
        float frameTime)
    {
        if ((position - movement.LastPosition).Length() > 0.05f)
        {
            movement.StuckTime = 0f;
            movement.LastPosition = position;
            return;
        }

        movement.StuckTime += frameTime;

        if (movement.StuckTime < StuckTimeout)
            return;

        FinishMovement(uid, body, movement, orders);
    }

    private void FinishMovement(EntityUid uid, PhysicsComponent body, RtsMovementComponent movement, RtsOrderQueueComponent orders)
    {
        movement.Target = null;
        movement.StuckTime = 0f;
        _physics.SetLinearVelocity(uid, Vector2.Zero, body: body);

        if (orders.Queue.Count > 0)
        {
            var next = orders.Queue[0];
            orders.Queue.RemoveAt(0);
            movement.Target = next.Position;
            Dirty(uid, movement);
            return;
        }

        // Клиент рисует по этому полю маркер назначения, поэтому его сброс обязан долететь.
        Dirty(uid, movement);

        if (orders.State == RtsUnitState.HoldPosition)
            return;

        orders.State = RtsUnitState.Idle;
        Dirty(uid, orders);
    }

    private void Decelerate(EntityUid uid, PhysicsComponent body, RtsMovementComponent movement, float frameTime)
    {
        if (body.LinearVelocity == Vector2.Zero)
            return;

        var velocity = Vector2.Lerp(body.LinearVelocity, Vector2.Zero, Math.Min(movement.Acceleration * frameTime / movement.Speed, 1f));

        if (velocity.Length() < 0.05f)
            velocity = Vector2.Zero;

        _physics.SetLinearVelocity(uid, velocity, body: body);
    }
}
