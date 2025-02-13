
namespace Content.Shared.ADT.btr;

[RegisterComponent]
public sealed partial class APCFuelTankComponent : Component
{
    [DataField]
    public bool Broken = false;
    [DataField]
    public bool HasFuel = false;

    public EntityUid? APC;
}
