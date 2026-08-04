using Robust.Shared.Serialization;

namespace Content.Shared.ADT.RTS;

/// <summary>
/// Стадия жизненного цикла RTS-матча.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §2.2.
/// </summary>
[Serializable, NetSerializable]
public enum RtsMatchStage : byte
{
    /// <summary>
    /// Карта создаётся и прогружается порциями. Игроки ещё не подключены к камерам.
    /// </summary>
    Generating,

    /// <summary>
    /// Карта готова, игроки видят свою базу, приказы заблокированы.
    /// </summary>
    Countdown,

    /// <summary>
    /// Собственно матч.
    /// </summary>
    Running,

    /// <summary>
    /// Победа определена, показывается результат.
    /// </summary>
    Finished,

    /// <summary>
    /// Игроки возвращаются в призраков, карта удаляется.
    /// </summary>
    Cleanup,
}

/// <summary>
/// Три ресурса RTS.
/// </summary>
[Serializable, NetSerializable]
public enum RtsResource : byte
{
    Wood,
    Wheat,
    Ore,
}

/// <summary>
/// Класс брони. Вместе с классом урона задаёт множитель по таблице из §8.4 плана.
/// </summary>
[Serializable, NetSerializable]
public enum RtsArmorClass : byte
{
    Unarmored,
    Light,
    Medium,
    Heavy,
    Building,
}

/// <summary>
/// Что юнит делает прямо сейчас.
/// </summary>
[Serializable, NetSerializable]
public enum RtsUnitState : byte
{
    Idle,
    Moving,
    AttackMove,
    Attacking,
    Chasing,
    Gathering,
    Returning,
    Building,
    Repairing,
    Following,
    HoldPosition,
}

/// <summary>
/// Тип приказа, который клиент просит выполнить.
/// </summary>
[Serializable, NetSerializable]
public enum RtsOrderType : byte
{
    /// <summary>
    /// Правый клик: сервер сам решает, что имелось в виду, по цели под курсором.
    /// </summary>
    Contextual,
    Move,
    AttackMove,
    Attack,
    Stop,
    HoldPosition,
    Patrol,
}

/// <summary>
/// Причина завершения матча. Нужна для текста на экране результата и для логов.
/// </summary>
[Serializable, NetSerializable]
public enum RtsMatchEndReason : byte
{
    /// <summary>
    /// Все здания и рабочие команды уничтожены.
    /// </summary>
    Eliminated,

    /// <summary>
    /// Игрок вышел из матча сам.
    /// </summary>
    Surrender,

    /// <summary>
    /// Игрок отключился и не вернулся.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Матч прерван админом или концом раунда.
    /// </summary>
    Aborted,
}
