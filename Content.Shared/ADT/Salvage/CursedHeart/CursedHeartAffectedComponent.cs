using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.Salvage.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CursedHeartAffectedComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<EntProtoId, EntityUid> Actions = new();

    [ViewVariables, AutoNetworkedField]
    public TimeSpan LastPumped = TimeSpan.Zero;

    [ViewVariables, AutoNetworkedField]
    public bool IsStopped = false;

    [DataField, AutoNetworkedField]
    public float PumpIntervalSeconds = 5f;

    [DataField, AutoNetworkedField]
    public float BloodDamageOnMiss = -50f;

    [DataField, AutoNetworkedField]
    public float BloodRegenOnPump = 17f;

    [DataField, AutoNetworkedField]
    public DamageSpecifier? PumpHealing;

    [DataField, AutoNetworkedField]
    public SoundSpecifier HeartbeatSound = new SoundPathSpecifier("/Audio/ADT/Heretic/heartbeat.ogg");
}
