using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.GameObjects;

namespace Content.Shared.ADT.RTS;

/// <summary>
/// Игрок просит выпустить его из матча. Сервер трактует это как сдачу.
/// Клиент никогда не решает исход сам — только шлёт намерение.
/// </summary>
[Serializable, NetSerializable]
public sealed class RtsLeaveMatchEvent : EntityEventArgs
{
}

/// <summary>
/// Приказ выделенным юнитам.
///
/// Выделение живёт целиком на клиенте, поэтому сервер обязан перепроверить каждую
/// сущность из списка: принадлежит ли она команде отправителя и жива ли вообще.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §4.2.
/// </summary>
[Serializable, NetSerializable]
public sealed class RtsOrderEvent : EntityEventArgs
{
    public NetEntity[] Units = Array.Empty<NetEntity>();

    public RtsOrderType Type;

    public NetCoordinates? Target;

    public NetEntity? TargetEntity;

    /// <summary>
    /// Приказ добавляется в очередь, а не заменяет текущий.
    /// </summary>
    public bool Queue;
}
