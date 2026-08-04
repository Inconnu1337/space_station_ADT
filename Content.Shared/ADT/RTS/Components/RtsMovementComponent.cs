using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.ADT.RTS.Components;

/// <summary>
/// Движение RTS-юнита. Не InputMover: у юнита нет игрока за спиной, а нужен нам только
/// вектор «куда идти», который каждый тик выдаёт flow field или прямой путь.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §7.2.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsMovementComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Speed = 3.5f;

    /// <summary>
    /// Насколько быстро юнит набирает и гасит скорость, тайлов в секунду за секунду.
    /// </summary>
    [DataField]
    public float Acceleration = 24f;

    /// <summary>
    /// Куда юнит идёт. Null — стоит на месте.
    ///
    /// Сетевое: по нему клиент рисует маркер точки назначения и линию от юнита к ней.
    /// Трафика почти нет — поле меняется только в момент выдачи приказа и по прибытии.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Vector2? Target;

    /// <summary>
    /// Насколько близко к цели считается «пришёл». У группы радиус увеличивается,
    /// иначе юниты толкутся, пытаясь встать в одну точку.
    /// </summary>
    [ViewVariables]
    public float ArrivalDistance = 0.35f;

    /// <summary>
    /// Сколько времени юнит не может продвинуться. Нужно, чтобы бросить безнадёжный путь
    /// и не толкаться в стену вечно.
    /// </summary>
    [ViewVariables]
    public float StuckTime;

    [ViewVariables]
    public Vector2 LastPosition;

    /// <summary>
    /// Вектор расталкивания от соседей. Считается реже, чем движение, и кэшируется здесь.
    /// </summary>
    [ViewVariables]
    public Vector2 Separation;
}

/// <summary>
/// Текущее состояние и очередь приказов юнита.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsOrderQueueComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public RtsUnitState State = RtsUnitState.Idle;

    /// <summary>
    /// Очередь приказов, поставленных с Shift. Не более <see cref="MaxQueue"/> штук.
    /// </summary>
    [ViewVariables]
    public List<RtsQueuedOrder> Queue = new();

    public const int MaxQueue = 8;
}

/// <summary>
/// Один отложенный приказ.
/// </summary>
public struct RtsQueuedOrder
{
    public RtsOrderType Type;
    public Vector2 Position;
    public EntityUid? Target;
}
