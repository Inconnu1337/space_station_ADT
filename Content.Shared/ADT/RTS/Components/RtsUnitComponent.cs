using Robust.Shared.GameStates;

namespace Content.Shared.ADT.RTS.Components;

/// <summary>
/// Сверхлёгкий RTS-юнит. Ничего мобного: ни Damageable, ни MobState, ни тела, ни атмоса.
/// См. Docs/ADT/RTS/RTS_MASTER_PLAN.md §8.7.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsUnitComponent : Component
{
    /// <summary>
    /// Сколько населения занимает юнит.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PopulationCost = 1;

    /// <summary>
    /// Радиус юнита в тайлах. Используется для расталкивания и для размера маркера выделения.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Radius = 0.35f;
}

/// <summary>
/// Сущность можно выделить рамкой или кликом.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsSelectableComponent : Component
{
    /// <summary>
    /// Приоритет при клике: из нескольких сущностей под курсором берётся больший.
    /// Боевые юниты важнее рабочих, рабочие важнее зданий.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Priority;

    /// <summary>
    /// Радиус маркера выделения в тайлах.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MarkerRadius = 0.45f;
}

/// <summary>
/// Здоровье RTS-сущности. Своё, а не Damageable: нам нужны целые числа, класс брони и
/// ноль подписок на апстримные системы урона.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RtsHealthComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Health = 50;

    [DataField, AutoNetworkedField]
    public int MaxHealth = 50;

    [DataField, AutoNetworkedField]
    public RtsArmorClass ArmorClass = RtsArmorClass.Unarmored;

    /// <summary>
    /// Плоское снижение входящего урона.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Armor;
}
