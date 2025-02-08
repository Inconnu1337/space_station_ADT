using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Content.Shared.ADT.btr;

[RegisterComponent]
public sealed partial class APCControllerComponent : Component
{
    [ViewVariables]
    public EntityUid? CurrentUser;

    [ViewVariables]
    public EntityUid? CurrentAPC;
}
