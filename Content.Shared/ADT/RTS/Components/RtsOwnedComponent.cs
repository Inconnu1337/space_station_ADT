using Robust.Shared.GameStates;

namespace Content.Shared.ADT.RTS.Components;

/// <summary>
/// Помечает сущность как принадлежащую команде RTS. Висит на всех юнитах и зданиях.
/// Наличие этого компонента — маркер для теста, запрещающего мобные и атмосферные
/// компоненты на RTS-сущностях (см. Docs/ADT/RTS/RTS_MASTER_PLAN.md §8.7).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsOwnedComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid? Team;

    /// <summary>
    /// Дублируется из команды, чтобы клиент мог красить сущность без запроса команды.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int TeamIndex;
}
