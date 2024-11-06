using System.Numerics;
using Content.Shared.Emag.Systems;
using Content.Shared.NinjaUpdate;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.NinjaUpdate;

public sealed class SpiderScannerSystem: SharedSpiderScannerSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpiderScannerComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SpiderScannerComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<SpiderScannerComponent, SpiderScannerPryFinished>(OnSpiderScannerPryFinished);

        SubscribeLocalEvent<SpiderScannerComponent, AppearanceChangeEvent>(OnAppearanceChange);
        SubscribeLocalEvent<InsideSpiderScannerComponent, ComponentStartup>(OnSpiderScannerInsertion);
        SubscribeLocalEvent<InsideSpiderScannerComponent, ComponentRemove>(OnSpiderScannerRemoval);
    }

    private void OnSpiderScannerInsertion(EntityUid uid, InsideSpiderScannerComponent component, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var spriteComponent))
        {
            return;
        }

        component.PreviousOffset = spriteComponent.Offset;
        spriteComponent.Offset = new Vector2(0, 1);
    }

    private void OnSpiderScannerRemoval(EntityUid uid, InsideSpiderScannerComponent component, ComponentRemove args)
    {
        if (!TryComp<SpriteComponent>(uid, out var spriteComponent))
        {
            return;
        }

        spriteComponent.Offset = component.PreviousOffset;
    }

    private void OnAppearanceChange(EntityUid uid, SpiderScannerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (!_appearance.TryGetData<bool>(uid, SpiderScannerComponent.SpiderScannerVisuals.ContainsEntity, out var isOpen, args.Component)
            || !_appearance.TryGetData<bool>(uid, SpiderScannerComponent.SpiderScannerVisuals.IsOn, out var isOn, args.Component))
        {
            return;
        }

        if (isOpen)
        {
            args.Sprite.LayerSetState(SpiderScannerVisualLayers.Base, "pod-open");
            args.Sprite.LayerSetVisible(SpiderScannerVisualLayers.Cover, false);
            args.Sprite.DrawDepth = (int) DrawDepth.Objects;
        }
        else
        {
            args.Sprite.DrawDepth = (int) DrawDepth.Mobs;
            args.Sprite.LayerSetState(SpiderScannerVisualLayers.Base, isOn ? "pod-on" : "pod-off");
            args.Sprite.LayerSetState(SpiderScannerVisualLayers.Cover, isOn ? "cover-on" : "cover-off");
            args.Sprite.LayerSetVisible(SpiderScannerVisualLayers.Cover, true);
        }
    }
}

public enum SpiderScannerVisualLayers : byte
{
    Base,
    Cover,
}
