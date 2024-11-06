using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared.NinjaUpdate;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class InsideSpiderScannerComponent: Component
{
    [ViewVariables]
    [DataField("previousOffset")]
    public Vector2 PreviousOffset { get; set; } = new(0, 0);
}
