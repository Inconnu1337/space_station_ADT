using JetBrains.Annotations;

namespace Content.Client.ADT.btr.UI;


[UsedImplicitly]
public sealed class APCControlBui : BoundUserInterface
{
    private APCControlWindow _window;

    public APCControlBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _window = new APCControlWindow();
    }

    protected override void Open()
    {
        base.Open();
        _window.OnClose += Close;

        _window.OpenCentered();

    }

    protected override void Dispose(bool disposing)
    {
        _window?.Close();
        base.Dispose(disposing);
    }
}
