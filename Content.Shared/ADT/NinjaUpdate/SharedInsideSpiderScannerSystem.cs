using Content.Shared.Standing;
using Robust.Shared.Containers;

namespace Content.Shared.NinjaUpdate;

public abstract partial class SharedSpiderScannerSystem
{
    public virtual void InitializeInsideSpiderScanner()
    {
        SubscribeLocalEvent<InsideSpiderScannerComponent, DownAttemptEvent>(HandleDown);
        SubscribeLocalEvent<InsideSpiderScannerComponent, EntGotRemovedFromContainerMessage>(OnEntGotRemovedFromContainer);
    }

    private void HandleDown(EntityUid uid, InsideSpiderScannerComponent component, DownAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnEntGotRemovedFromContainer(EntityUid uid, InsideSpiderScannerComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (Terminating(uid))
        {
            return;
        }

        RemComp<InsideSpiderScannerComponent>(uid);
    }
}
