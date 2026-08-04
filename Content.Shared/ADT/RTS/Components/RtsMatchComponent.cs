using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.ADT.RTS.Components;

/// <summary>
/// Состояние одного RTS-матча. Живёт на отдельной сущности, а не на карте, чтобы
/// пережить пересоздание карты при неудачной генерации.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §2.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsMatchComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public RtsMatchStage Stage = RtsMatchStage.Generating;

    /// <summary>
    /// Выключается третьим аргументом initrts. При false туман войны не считается вообще.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool FogOfWar = true;

    /// <summary>
    /// Карта арены. Удаляется вместе с матчем.
    /// </summary>
    [ViewVariables]
    public EntityUid? MapUid;

    [ViewVariables]
    public MapId MapId = MapId.Nullspace;

    [ViewVariables]
    public int Seed;

    /// <summary>
    /// Размер арены в тайлах. Арена всегда квадратная и центрирована в нуле.
    /// </summary>
    [ViewVariables]
    public Vector2i ArenaSize = new(192, 192);

    /// <summary>
    /// Сущности команд (<see cref="RtsTeamComponent"/>), по одной на игрока.
    /// </summary>
    [ViewVariables]
    public List<EntityUid> Teams = new();

    /// <summary>
    /// Момент, когда текущая стадия должна смениться. Используется в Countdown и Finished.
    /// </summary>
    [ViewVariables]
    public TimeSpan StageEnd;

    /// <summary>
    /// Сколько строк тайлов арены уже сгенерировано. Генерация идёт порциями по тикам,
    /// чтобы не ронять тикрейт станции.
    /// </summary>
    [ViewVariables]
    public int GeneratedRows;

    /// <summary>
    /// Номер попытки генерации. Если арена получилась несвязной, пробуем новый seed.
    /// </summary>
    [ViewVariables]
    public int Attempt;

    [ViewVariables]
    public EntityUid? Winner;

    [ViewVariables]
    public RtsMatchEndReason EndReason = RtsMatchEndReason.Aborted;
}
