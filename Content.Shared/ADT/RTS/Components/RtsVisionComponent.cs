using Robust.Shared.GameStates;

namespace Content.Shared.ADT.RTS.Components;

/// <summary>
/// Радиус обзора сущности. Круг без трассировки препятствий: честный occlusion как у
/// StationAi для арены 192x192 неоправданно дорог, а на игру почти не влияет.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §6.2.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsVisionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Range = 7f;
}
