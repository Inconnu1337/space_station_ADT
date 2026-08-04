using Robust.Shared.GameStates;

namespace Content.Shared.ADT.RTS.Components;

/// <summary>
/// Сущность-камера игрока в RTS. Это НЕ призрак: у призрака полный обзор и он игнорирует
/// маски видимости, что сломало бы туман войны. См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §4.1.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsCameraComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Team;

    /// <summary>
    /// Индекс команды. Дублирует <see cref="Team"/> намеренно: сущность команды живёт в
    /// нулевом пространстве, и полагаться на то, что клиент её получит, нельзя. Всё
    /// определение «своё или чужое» на клиенте идёт по этому числу.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int TeamIndex = -1;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? Match;

    /// <summary>
    /// Границы арены в координатах карты. По ним же клиент знает размер поля тумана.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Box2 Bounds;

    /// <summary>
    /// Включён ли туман войны в этом матче. Дублируется сюда из матча по той же причине,
    /// что и <see cref="TeamIndex"/>: сущность матча живёт в нулевом пространстве.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool FogOfWar = true;
}
