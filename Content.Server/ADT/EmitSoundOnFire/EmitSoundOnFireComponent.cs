using Robust.Shared.Audio;

namespace Content.Server.ADT.EmitSoundOnFire;

[RegisterComponent]
public sealed partial class EmitSoundOnFireComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    [DataField]
    public int MinInterval = 3;

    [DataField]
    public int MaxInterval = 7;

    [ViewVariables]
    public TimeSpan NextSound = TimeSpan.Zero;
}
