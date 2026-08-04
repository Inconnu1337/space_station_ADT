using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared.ADT.RTS.Components;

/// <summary>
/// Команда одного игрока в матче: ресурсы, население, привязка к сессии и камере.
/// Состояние летит только владельцу через PVS-override.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §5.3.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsTeamComponent : Component
{
    /// <summary>
    /// Матч, которому принадлежит команда.
    /// </summary>
    [ViewVariables]
    public EntityUid Match;

    /// <summary>
    /// Индекс команды в матче. Задаёт цвет и бит видимости.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int Index;

    [ViewVariables, AutoNetworkedField]
    public Color Color = Color.LimeGreen;

    [ViewVariables, AutoNetworkedField]
    public int Wood;

    [ViewVariables, AutoNetworkedField]
    public int Wheat;

    [ViewVariables, AutoNetworkedField]
    public int Ore;

    /// <summary>
    /// Общий лимит склада на каждый ресурс. Растёт от складов и амбаров.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int StorageCap = 10000;

    [ViewVariables, AutoNetworkedField]
    public int Population;

    [ViewVariables, AutoNetworkedField]
    public int PopulationCap;

    /// <summary>
    /// Жёсткий потолок населения, выше которого дома уже не поднимают лимит.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public int PopulationHardCap = 100;

    [ViewVariables, AutoNetworkedField]
    public bool Defeated;

    /// <summary>
    /// Игрок, управляющий командой.
    /// </summary>
    [ViewVariables]
    public NetUserId? User;

    /// <summary>
    /// Разум игрока. Нужен, чтобы вернуть управление призраку при выходе.
    /// </summary>
    [ViewVariables]
    public EntityUid? MindId;

    /// <summary>
    /// Призрак, из которого игрок зашёл в матч.
    /// </summary>
    [ViewVariables]
    public EntityUid? Ghost;

    /// <summary>
    /// Сущность-камера этого игрока.
    /// </summary>
    [ViewVariables]
    public EntityUid? Camera;

    /// <summary>
    /// Тайл, вокруг которого стоит стартовая база.
    /// </summary>
    [ViewVariables]
    public Vector2i SpawnTile;

    /// <summary>
    /// Момент, после которого отключившийся игрок получает техническое поражение.
    /// </summary>
    [ViewVariables]
    public TimeSpan? DisconnectDeadline;

    public int GetResource(RtsResource resource)
    {
        switch (resource)
        {
            case RtsResource.Wood:
                return Wood;
            case RtsResource.Wheat:
                return Wheat;
            case RtsResource.Ore:
                return Ore;
            default:
                return 0;
        }
    }
}
