using Robust.Shared.Input;

namespace Content.Shared.ADT.RTS.Input;

/// <summary>
/// Бинды RTS-режима. Живут в отдельном input-контексте "rts", где нет станционных
/// действий — именно поэтому правый клик свободен под приказы.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §4.2.
/// </summary>
[KeyFunctions]
public static class RtsKeyFunctions
{
    /// <summary>
    /// Выделение: клик по юниту, протяжка — рамка, с Shift — добавить к выделению.
    /// </summary>
    public static readonly BoundKeyFunction RtsSelect = "RtsSelect";

    /// <summary>
    /// Контекстный приказ: идти, атаковать, собирать, чинить.
    /// </summary>
    public static readonly BoundKeyFunction RtsOrder = "RtsOrder";

    /// <summary>
    /// Явная атака по точке.
    /// </summary>
    public static readonly BoundKeyFunction RtsAttackMove = "RtsAttackMove";

    public static readonly BoundKeyFunction RtsStop = "RtsStop";

    public static readonly BoundKeyFunction RtsHoldPosition = "RtsHoldPosition";

    public static readonly BoundKeyFunction RtsPatrol = "RtsPatrol";

    /// <summary>
    /// Прыжок камеры к простаивающему рабочему.
    /// </summary>
    public static readonly BoundKeyFunction RtsIdleWorker = "RtsIdleWorker";

    /// <summary>
    /// Прыжок камеры к последнему событию (атака на базу).
    /// </summary>
    public static readonly BoundKeyFunction RtsLastEvent = "RtsLastEvent";

    /// <summary>
    /// Покинуть матч и вернуться в призрака.
    /// </summary>
    public static readonly BoundKeyFunction RtsLeaveMatch = "RtsLeaveMatch";
}
