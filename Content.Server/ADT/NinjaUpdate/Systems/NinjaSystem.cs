using Content.Shared.Ninja.Components;
using Robust.Shared.GameObjects;
using Content.Server.Station.Systems;
using Content.Shared.Popups;

namespace Content.Server.ADT.NinjaUpdate.EntitySystems;

public sealed partial class NinjaSystem : EntitySystem
{
    #region Dependency
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly StationSystem _station = default!;
    #endregion

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpaceNinjaComponent, ComponentStartup>(OnStartup);

        InitializeUsefulNinjaAbilities();
    }
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
    }

    private void OnStartup(EntityUid uid, SpaceNinjaComponent component, ComponentStartup args)
    {

    }

}
