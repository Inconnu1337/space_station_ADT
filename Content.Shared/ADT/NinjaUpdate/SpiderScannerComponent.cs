using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.NinjaUpdate;

[RegisterComponent, NetworkedComponent]
public sealed partial class SpiderScannerComponent : Component
{
    [ViewVariables]
    public bool IsScanning = false;
    /// <summary>
    ///     Delay applied when inserting a mob in the pod.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("entryDelay")]
    public float EntryDelay = 2f;

    /// <summary>
    /// Delay applied when trying to pry open a locked pod.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("pryDelay")]
    public float PryDelay = 5f;

    /// <summary>
    /// Container for mobs inserted in the Spider Scanner
    /// </summary>
    [ViewVariables]
    public ContainerSlot BodyContainer = default!;

    [Serializable, NetSerializable]
    public enum SpiderScannerVisuals : byte
    {
        ContainsEntity,
        IsOn
    }
}
