using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ADT.btr;

[RegisterComponent]
public sealed partial class APCEntityComponent : Component
{
    public EntityUid? MapEnt;
    public EntityUid? GridEnt;
    public EntityUid? APC;

    [DataField("entryDelay")]
    public float EntryDelay = 2f;
    [DataField("exitDelay")]
    public float ExitDelay = 2f;

    [DataField("accessDeniedSound")]
    public SoundSpecifier AccessDeniedSound = new SoundPathSpecifier("/Audio/Machines/Nuke/angry_beep.ogg");

    [DataField("entrysound")]
    public SoundSpecifier EntrySound = new SoundPathSpecifier("/Audio/ADT/Mecha/nominal.ogg");

    [DataField]
    public string? BaseState;
    [DataField]
    public string? DestroyedState;

    [DataField]
    public bool Destroyed = false;

    [DataField]
    public string GridPath = "/Maps/ADTMaps/Shuttles/ERT/default.yml";
    [DataField]
    public int MaxOnAPC = 3;
    [DataField]
    public int OnAPC;

// apc control:

    [ViewVariables]
    public EntityUid? User;

    [ViewVariables]
    public EntityUid? Controller;// ??? why?

    [DataField("APCControlReturnAction")]
    public EntProtoId APCControlReturnAction = "APCControlReturnAction";

    [DataField("APCControlReturnActionEntity")]
    public EntityUid? APCControlReturnActEntity;
}
